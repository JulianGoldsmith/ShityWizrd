using UnityEngine;
using Fusion;
using System.Collections.Generic;
public class PlayerVoiceSource : NetworkBehaviour
{
    // Defines the source of a player voice within the world.
    // Only the auditory aspect of it.

    // define the listening (local) player transform,
    // so that we can do proximity chat.
    public static Transform listening_player;

    const float distance_scaling = 0.1f;

    public Transform follow_target;

    public AudioSource audioSource;

    // shared dict for ease of lookup
    public static Dictionary<PlayerRef, PlayerVoiceSource> player_voice_sources = null;


    private void LateUpdate()
    {
        FollowTarget();
        ProximityVolume();
    }

    void FollowTarget()
    {
        if (follow_target != null)
            transform.position = follow_target.position;
    }

    public void ProximityVolume()
    {
        // lower source volume by distance.
        //audioSource.volume = ConvertDistanceToVolume(DistanceToListener());
    }
    float DistanceToListener()
    {
        return (listening_player.position - transform.position).magnitude;
    }
    float ConvertDistanceToVolume(float distance)
    {
        // negative exponential so that at zero distance it is
        // full volume, and trails off with distance.
        // At a distance of 1/distance_scaling, volume will be 67%.
        return Mathf.Clamp01(Mathf.Exp(-distance * distance_scaling));
    }

    public override void Spawned()
    {
        base.Spawned();

        // input authority is the owner.
        PlayerRef playerRef = Object.InputAuthority;

        // don't need to call ourselves:
        if (playerRef == Runner.LocalPlayer)
        {
            listening_player = transform;
            return;
        }

        // Subscribe to dictionary so connector can find it.
        SubscribeVoice(playerRef);

        if (Runner.LocalPlayer.PlayerId > playerRef.PlayerId)
            // Rather than only run on host, we run on whoever has the lower
            // player id. This ensures only one of each pair sets up the connection.
            return;

        // Start WebRTC audio call with that player.
        // only one of the pair should do this.
        NetworkSignaling.instance.connector.StartCallWith(playerRef);
    }

    void SubscribeVoice(PlayerRef playerRef)
    {
        if (player_voice_sources == null)
            player_voice_sources = new Dictionary<PlayerRef, PlayerVoiceSource>();

        if (player_voice_sources.ContainsKey(playerRef))
            return;
        player_voice_sources[playerRef] = this;
    }

    public static PlayerVoiceSource Get(PlayerRef playerRef)
    {
        return player_voice_sources[playerRef];
    }
}
