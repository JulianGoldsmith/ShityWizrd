using UnityEngine;
using Unity.WebRTC;
using Fusion;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

public class WebRTCConnector : MonoBehaviour
{
    /* 
     Set up and handle the WebRTC connection including sending
     and receiving audio for voice chat. 
    */
    public const string default_ice_server = "stun:stun.l.google.com:19302";

    Dictionary<PlayerRef, ConnectedVoice> connected_voices = new Dictionary<PlayerRef, ConnectedVoice>();

    [SerializeField] private AudioSource inputAudioSource;
    //[SerializeField] private AudioSource outputAudioSource;

    [SerializeField] NetworkSignaling network_signaling;

    //private RTCPeerConnection peerConnection;
    private MediaStream _sendStream;
    //private MediaStream _receiveStream;

    private AudioClip clipInput;
    private AudioStreamTrack micTrack;

    [SerializeField] int samplingFrequency = 48000;
    [SerializeField] int lengthSeconds = 1;

    private string m_deviceName = null;

    bool is_muted = false;
    bool prior_is_muted_state = false;

    private void OnGUI()
    {
        //if (!is_connected && GUI.Button(new Rect(0, 80, 200, 40), $"Conn VC {other_player_ref}"))
        //{
        //    StartCallWith(other_player_ref);
        //    is_connected = true;
        //}
        //if (is_connected && GUI.Button(new Rect(0, 80, 200, 40), $"Discon VC {other_player_ref}"))
        //{
        //    OnDisconnect(other_player_ref);
        //    is_connected = false;
        //}
        is_muted = GUI.Toggle(new Rect(150, 80, 50, 40), is_muted, "Mute");
        GUI.TextArea(new Rect(0, 80, 150, 40), m_deviceName);
    }

    private void Start()
    {
        connected_voices = new Dictionary<PlayerRef, ConnectedVoice>();

        if(Microphone.devices.Length <= 0)
        {
            Debug.LogError("No microphones connected.");
            return;
        }

        m_deviceName = Microphone.devices[0];

        OnStartMic();
    }
    
    private void Update()
    {
        WebRTC.Update();
        if (prior_is_muted_state != is_muted)
        {
            if (is_muted)
            {
                OnPause();
            }
            else
            {
                OnResume();
            }
        }
        prior_is_muted_state = is_muted;
    }

    void OnStartMic()
    {
        if (m_deviceName == "")
            return;

        clipInput = Microphone.Start(m_deviceName, true, lengthSeconds, samplingFrequency);
        // set the latency to “0” samples before the audio starts to play.
        while (!(Microphone.GetPosition(m_deviceName) > 0)) { }
        
        inputAudioSource.loop = true;
        inputAudioSource.clip = clipInput;
        inputAudioSource.Play();

        micTrack = new AudioStreamTrack(inputAudioSource);
    }

    void handleSendChannelStatusChange()
    {
        Debug.Log("Send channel status changed");
    }

    void SendIceCandidate(PlayerRef target, RTCIceCandidate candidate)
    {
        Debug.Log($"ICE: {candidate.Candidate}");
        network_signaling.SendIceCandidate(target, candidate);
    }

    public void AddIceCandidate(PlayerRef source, string candidate, string sdpMid, int sdpMLineIndex)
    {
        RTCIceCandidate ice_candidate = new RTCIceCandidate(new RTCIceCandidateInit()
        {
            candidate = candidate,
            sdpMid = sdpMid,
            sdpMLineIndex = sdpMLineIndex
        });
        ConnectedVoice cv = GetCV(source);
        if (cv != null)
            cv.peerConnection.AddIceCandidate(ice_candidate);
    }

    //void OnAddTrack(MediaStreamTrackEvent e)
    //{
    //    var track = e.Track as AudioStreamTrack;
    //    outputAudioSource.SetTrack(track);
    //    outputAudioSource.loop = true;
    //    outputAudioSource.Play();
    //}

    // 1. local creates offer
    // 2. on success, assigns local description.
    // 3. sends offer to peer
    // 4. peer receives offer.
    // 5. peer assigns offer to remote description
    // 6. peer creates answer
    // 7. on success, assigns local description.
    // 8. sends answer to local.
    // 9. local receives answer
    // 10. local assigns answer to remote description.

    ConnectedVoice BaseConnectedVoice(PlayerRef target)
    {
        ConnectedVoice cv = new ConnectedVoice();
        cv.playerVoiceSource = PlayerVoiceSource.Get(target);

        RTCConfiguration configuration = GetSelectedSdpSemantics();
        cv.receiveStream = new MediaStream();
        cv.receiveStream.OnAddTrack = e =>
        {
            if (e.Track is AudioStreamTrack track)
            {
                // `AudioSource.SetTrack` is a extension method which is available 
                // when using `Unity.WebRTC` namespace.
                cv.playerVoiceSource.audioSource.SetTrack(track);

                // Please do not forget to turn on the `loop` flag.
                cv.playerVoiceSource.audioSource.loop = true;
                cv.playerVoiceSource.audioSource.Play();
            }
        };

        return cv;
    }
    ConnectedVoice HostConnectedVoice(PlayerRef target)
    {
        ConnectedVoice cv = BaseConnectedVoice(target);
        RTCConfiguration configuration = GetSelectedSdpSemantics();
        cv.peerConnection = HostPeerConnection(cv, configuration, target);
        return cv;
    }
    ConnectedVoice ClientConnectedVoice(PlayerRef target)
    {
        ConnectedVoice cv = BaseConnectedVoice(target);
        RTCConfiguration configuration = GetSelectedSdpSemantics();
        cv.peerConnection = ClientPeerConnection(cv, configuration, target);
        return cv;
    }

    RTCPeerConnection BasePeerConnection(ConnectedVoice cv, RTCConfiguration configuration, PlayerRef target)
    {
        RTCPeerConnection conn = new RTCPeerConnection(ref configuration)
        {
            OnIceCandidate = candidate => SendIceCandidate(target, candidate),
            OnTrack = (RTCTrackEvent e) =>
            {
                if (e.Track.Kind == TrackKind.Audio)
                {
                    // Add track to MediaStream for receiver.
                    // This process triggers `OnAddTrack` event of `MediaStream`.
                    cv.receiveStream.AddTrack(e.Track);
                }
            }
        };
        //var transceiver = conn.AddTransceiver(TrackKind.Audio);
        //transceiver.Direction = RTCRtpTransceiverDirection.SendRecv;

        if (_sendStream == null)
            _sendStream = new MediaStream();
        conn.AddTrack(micTrack, _sendStream);

        return conn;
    }
    RTCPeerConnection HostPeerConnection(ConnectedVoice cv, RTCConfiguration configuration, PlayerRef target)
    {
        RTCPeerConnection conn = BasePeerConnection(cv, configuration, target);
        // uncomment if we need renegotiation, such as adding of new tracks. 
        //conn.OnNegotiationNeeded = () => StartCoroutine(CreateOffer(target));
        conn.OnIceConnectionChange = state => OnIceConnectionChange(state, target);
        return conn;
    }
    RTCPeerConnection ClientPeerConnection(ConnectedVoice cv, RTCConfiguration configuration, PlayerRef target)
    {
        RTCPeerConnection conn = BasePeerConnection(cv, configuration, target);
        conn.OnIceConnectionChange = state => { Debug.Log(state); };
        return conn;
    }


    public void StartCallWith(PlayerRef target)
    {
        ConnectedVoice cv = HostConnectedVoice(target);
        connected_voices.Add(target, cv);

        StartCoroutine(CreateOffer(target));
    }

    public IEnumerator CreateOffer(PlayerRef target)
    {
        // need to extend this to have a peerconnection per playerref in lobby.
        ConnectedVoice cv = GetCV(target);
        if (cv == null)
            yield break;
        RTCPeerConnection pc = cv.peerConnection;

        // 1. create an offer
        var op = pc.CreateOffer();
        yield return op;

        if (op.IsError)
        {
            Debug.LogError($"Unable to create offer. {op.Error.message}");
            yield break;
        }

        //if (pc.SignalingState != RTCSignalingState.Stable)
        //{
        //    Debug.LogError("Peerconnection state is not stable.");
        //    yield break;
        //}

        RTCSessionDescription local_desc = op.Desc;

        Debug.Log($"local: {local_desc.sdp}");

        // 2. successfully created offer,
        //      so assign to local description.
        var op2 = pc.SetLocalDescription(ref local_desc);
        yield return op2;

        if (op2.IsError)
        {
            Debug.LogError($"Unable to set local description after creating offer. {op2.Error.message}");
            yield break;
        }

        // 3. successfully assigned local description,
        //      so send offer to network peer.
        network_signaling.SendOffer(target, local_desc);
    }

    public IEnumerator OnReceivedOffer(PlayerRef source, string remote_desc)
    {
        // need to extend this to have a peerconnection per playerref in lobby.
        ConnectedVoice cv = ClientConnectedVoice(source);
        Debug.Log($"adding {source} to voices");
        Debug.Log($"{cv.peerConnection.GetTransceivers().First().Sender.Track} {cv.peerConnection.GetTransceivers().First().Sender.Track.Enabled}");
        connected_voices.Add(source, cv);

        RTCPeerConnection pc = cv.peerConnection;

        // rebuild the session description.
        RTCSessionDescription remote_session_desc = new RTCSessionDescription() { 
            type = RTCSdpType.Offer, sdp = remote_desc
        };
        Debug.Log($"remote: {remote_desc}");
        // 4. peer receives offer.
        // 5. peer assigns offer to remote description
        Debug.LogError("Setting remote description from offer");
        var op = pc.SetRemoteDescription(ref remote_session_desc);
        yield return op;
        if (op.IsError)
        {
            Debug.LogError("Unable to set remote description when received offer.");
            yield break;
        }

        // 6. peer creates answer
        var op2 = pc.CreateAnswer();
        yield return op2;
        if (op2.IsError)
        {
            Debug.LogError("Unable to create answer.");
            yield break;
        }

        // 7. on success, assigns local description.
        RTCSessionDescription local_desc = op2.Desc;
        Debug.Log($"local: {local_desc.sdp}");
        var op3 = pc.SetLocalDescription(ref local_desc);
        yield return op3;
        if (op3.IsError)
        {
            Debug.LogError("Unable to set local description when creating answer.");
            yield break;
        }

        // 8. sends answer to local.
        network_signaling.SendAnswer(source, local_desc);

        if (is_muted)
            OnPause();
        else
            OnResume();
    }

    public IEnumerator OnReceivedAnswer(PlayerRef source, string remote_desc)
    {
        // 9. local receives answer
        // need to extend this to have a peerconnection per playerref in lobby.
        ConnectedVoice cv = GetCV(source);
        if (cv == null)
            yield break;

        RTCPeerConnection pc = cv.peerConnection;

        // rebuild the session description.
        RTCSessionDescription remote_session_desc = new RTCSessionDescription()
        {
            type = RTCSdpType.Answer,
            sdp = remote_desc
        };
        Debug.Log($"remote: {remote_session_desc.sdp}");
        // 10. local assigns answer to remote description.
        var op = pc.SetRemoteDescription(ref remote_session_desc);
        yield return op;
        if (op.IsError)
        {
            Debug.LogError("Unable to set remote description when received answer.");
            yield break;
        }

        Debug.Log("P2P Handshake successful.");

        if (is_muted)
            OnPause();
        else
            OnResume();
    }



    ConnectedVoice GetCV(PlayerRef playerRef)
    {
        return connected_voices.TryGetValue(playerRef, out var result) ? result : null;
    }




    void OnPause()
    {
        if (connected_voices == null)
            return;
        foreach (var voice in connected_voices.Values)
        {
            var transceiver1 = voice.peerConnection.GetTransceivers().First();
            var track = transceiver1.Sender.Track;
            track.Enabled = false;
        }
    }

    void OnResume()
    {
        if (connected_voices == null)
            return;
        foreach (var voice in connected_voices.Values)
        {
            var transceiver1 = voice.peerConnection.GetTransceivers().First();
            var track = transceiver1.Sender.Track;
            track.Enabled = true;
        }
    }

    private void OnApplicationQuit()
    {
        OnDisconnectAll();
    }
    private void OnDisconnect(PlayerRef playerRef)
    {
        ConnectedVoice voice = GetCV(playerRef);
        if (voice == null)
            return;

        voice.peerConnection?.Dispose();
        voice.peerConnection = null;
        voice.playerVoiceSource?.audioSource?.Stop();

        connected_voices.Remove(playerRef);
    }
    void OnDisconnectAll()
    {
        Microphone.End(m_deviceName);
        clipInput = null;

        micTrack?.Dispose();
        _sendStream?.Dispose();        
        inputAudioSource.Stop();

        foreach (ConnectedVoice voice in connected_voices.Values)
        {
            voice.peerConnection?.Dispose();
            voice.peerConnection = null;
            voice.playerVoiceSource?.audioSource?.Stop();
        }

        connected_voices.Clear();
    }

    public static RTCConfiguration GetSelectedSdpSemantics()
    {
        RTCConfiguration config = default;
        config.iceServers = new[] {
            new RTCIceServer
            {
                urls = new[]
                {
                    //default_ice_server,
                    "stun:stun.relay.metered.ca:80"
                }
            },
            new RTCIceServer
            {
                urls = new[]
                {
                    "turn:global.relay.metered.ca:80",
                    "turn:global.relay.metered.ca:80?transport=udp",
                    "turn:global.relay.metered.ca:443",
                    "turns:global.relay.metered.ca:443?transport=udp"
                },
                username = "bd61df2c282bf4c6403f3a19",
                credential = "huRhOJN6vrCwYeyO",
                credentialType = RTCIceCredentialType.Password
            }
        };
        return config;
    }

    void OnIceConnectionChange(RTCIceConnectionState state, PlayerRef playerRef)
    {
        Debug.Log($"Ice Connection State changed with {playerRef} to {state}");
        //if (state == RTCIceConnectionState.Disconnected)
        //{
        //    // if disconnected try again.
        //    OnDisconnect(playerRef);
        //    StartCallWith(playerRef);
        //}
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        // Push raw audio into WebRTC immediately.
        // Should reduce latency (otherwise mic records in 
        // 1 second chunks).
        micTrack?.SetData(data, channels, samplingFrequency);
    }
}
