using UnityEngine;
using Unity.WebRTC;
using Fusion;
public class ConnectedVoice
{
    // A class to hold information about a connected voice
    // such as the WebRTC peerconnection, the PlayerVoiceSource (AudioSource)
    // it correlates to, etc.

    public PlayerRef playerRef;
    public RTCPeerConnection peerConnection;
    public MediaStream receiveStream;
    public PlayerVoiceSource playerVoiceSource;
}
