using UnityEngine;
using Unity.WebRTC;
using Fusion;
using System.Collections;
using System.Linq;

public class WebRTCConnector : MonoBehaviour
{
    /* 
     Set up and handle the WebRTC connection including sending
     and receiving audio for voice chat. 
    */
    public const string default_ice_server = "stun:stun.l.google.com:19302";

    [SerializeField] private AudioSource inputAudioSource;
    [SerializeField] private AudioSource outputAudioSource;

    [SerializeField] NetworkSignaling network_signaling;

    private RTCPeerConnection peerConnection;
    private MediaStream _sendStream;
    private MediaStream _receiveStream;

    private AudioClip clipInput;
    private AudioStreamTrack micTrack;

    [SerializeField] int samplingFrequency = 48000;
    [SerializeField] int lengthSeconds = 1;

    private string m_deviceName = null;

    bool is_muted = false;
    bool prior_is_muted_state = false;

    bool is_connected = false;

    public PlayerRef other_player_ref = PlayerRef.None;

    private void OnGUI()
    {
        if (other_player_ref == PlayerRef.None)
            return;

        if (!is_connected && GUI.Button(new Rect(0, 80, 200, 40), $"Conn VC {other_player_ref}"))
        {
            StartCallWith(other_player_ref);
            is_connected = true;
        }
        if (is_connected && GUI.Button(new Rect(0, 80, 200, 40), $"Discon VC {other_player_ref}"))
        {
            OnDisconnect(other_player_ref);
            is_connected = false;
        }
        is_muted = GUI.Toggle(new Rect(150, 120, 50, 40), is_muted, "Mute");
        GUI.TextArea(new Rect(0, 120, 150, 40), m_deviceName);
    }

    private void Start()
    {
        StartCoroutine(WebRTC.Update());
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
        // if you want to here your own input, you can uncomment this:
        inputAudioSource.Play();

        micTrack = new AudioStreamTrack(inputAudioSource);
    }

    public void StartCallWith(PlayerRef target)
    {
        _sendStream = new MediaStream();

        var configuration = GetSelectedSdpSemantics();
        peerConnection = new RTCPeerConnection(ref configuration)
        {
            OnIceCandidate = candidate => SendIceCandidate(target, candidate),
            OnNegotiationNeeded = () => StartCoroutine(CreateOffer(target)),
            OnIceConnectionChange = state => { Debug.Log(state); }
        };
        //var transceiver = peerConnection.AddTransceiver(TrackKind.Audio);
        //transceiver.Direction = RTCRtpTransceiverDirection.SendRecv;

        // set up the sending stream to send audio to peer.
        peerConnection.AddTrack(micTrack, _sendStream);


        StartCoroutine(CreateOffer(target));
    }

    void handleSendChannelStatusChange()
    {
        Debug.Log("Send channel status changed");
    }

    void SendIceCandidate(PlayerRef target, RTCIceCandidate candidate)
    {
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
        peerConnection.AddIceCandidate(ice_candidate);
    }

    void OnAddTrack(MediaStreamTrackEvent e)
    {
        var track = e.Track as AudioStreamTrack;
        outputAudioSource.SetTrack(track);
        outputAudioSource.loop = true;
        outputAudioSource.Play();
    }

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

    public IEnumerator CreateOffer(PlayerRef target)
    {
        // need to extend this to have a peerconnection per playerref in lobby.
        RTCPeerConnection pc = peerConnection;

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
        var configuration = GetSelectedSdpSemantics();
        _receiveStream = new MediaStream();
        _receiveStream.OnAddTrack = e =>
        {
            if (e.Track is AudioStreamTrack track)
            {
                // `AudioSource.SetTrack` is a extension method which is available 
                // when using `Unity.WebRTC` namespace.
                outputAudioSource.SetTrack(track);

                // Please do not forget to turn on the `loop` flag.
                outputAudioSource.loop = true;
                outputAudioSource.Play();
            }
        };

        peerConnection = new RTCPeerConnection(ref configuration)
        {
            OnIceCandidate = candidate => SendIceCandidate(source, candidate),
            OnIceConnectionChange = state => { Debug.Log(state); }
        };
        peerConnection.OnTrack = (RTCTrackEvent e) => {
            if (e.Track.Kind == TrackKind.Audio)
            {
                // Add track to MediaStream for receiver.
                // This process triggers `OnAddTrack` event of `MediaStream`.
                _receiveStream.AddTrack(e.Track);
            }
        };
        var transceiver2 = peerConnection.AddTransceiver(TrackKind.Audio);
        transceiver2.Direction = RTCRtpTransceiverDirection.RecvOnly;

        RTCPeerConnection pc = peerConnection;


        // rebuild the session description.
        RTCSessionDescription remote_session_desc = new RTCSessionDescription() { 
            type = RTCSdpType.Offer, sdp = remote_desc
        };

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
        var op3 = pc.SetLocalDescription(ref local_desc);
        yield return op3;
        if (op3.IsError)
        {
            Debug.LogError("Unable to set local description when creating answer.");
            yield break;
        }

        // 8. sends answer to local.
        network_signaling.SendAnswer(source, local_desc);
    }

    public IEnumerator OnReceivedAnswer(PlayerRef source, string remote_desc)
    {
        // 9. local receives answer
        // need to extend this to have a peerconnection per playerref in lobby.
        RTCPeerConnection pc = peerConnection;

        // rebuild the session description.
        RTCSessionDescription remote_session_desc = new RTCSessionDescription()
        {
            type = RTCSdpType.Answer,
            sdp = remote_desc
        };

        // 10. local assigns answer to remote description.
        var op = pc.SetRemoteDescription(ref remote_session_desc);
        yield return op;
        if (op.IsError)
        {
            Debug.LogError("Unable to set remote description when received answer.");
            yield break;
        }

        Debug.Log("P2P Handshake successful.");
    }






    void OnPause()
    {
        if (peerConnection == null)
            return;

        var transceiver1 = peerConnection.GetTransceivers().First();
        var track = transceiver1.Sender.Track;
        track.Enabled = false;
    }

    void OnResume()
    {
        if (peerConnection == null)
            return;

        var transceiver1 = peerConnection.GetTransceivers().First();
        var track = transceiver1.Sender.Track;
        track.Enabled = true;
    }

    private void OnApplicationQuit()
    {
        OnDisconnect(other_player_ref);
    }

    void OnDisconnect(PlayerRef target)
    {
        Microphone.End(m_deviceName);
        clipInput = null;

        micTrack?.Dispose();
        _receiveStream?.Dispose();
        _sendStream?.Dispose();
        peerConnection?.Dispose();
        
        peerConnection = null;

        inputAudioSource.Stop();
        outputAudioSource.Stop();
    }

    public static RTCConfiguration GetSelectedSdpSemantics()
    {
        RTCConfiguration config = default;
        config.iceServers = new[] { new RTCIceServer { urls = new[] { default_ice_server } } };

        return config;
    }
}
