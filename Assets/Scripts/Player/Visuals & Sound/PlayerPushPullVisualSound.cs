using System;
using UnityEngine;

public class PlayerPushPullVisualSound : MonoBehaviour
{
    [SerializeField] private PlayerPushPull playerPushPull;
    [SerializeField] private AudioSource pushAudioSource;
    [SerializeField] private AudioSource pullAudioSource;
    [SerializeField] private Animator handAnimator;

    private const string PUSH_ANIMATION_TRIGGER = "PUSH";
    private const string PULL_ANIMATION_TRIGGER = "PULL";
    private const string IS_PUSHING_BOOL = "isPushing";
    private const string IS_PULLING_BOOL = "isPulling";

    private void Start()
    {
        playerPushPull.OnPush += HandlePush;
        playerPushPull.OnPull += HandlePull;
        playerPushPull.OnStopPush += HandleStopPush;
        playerPushPull.OnStopPull += HandleStopPull;
    }

    private void HandleStopPull()
    {
        handAnimator.SetBool(IS_PULLING_BOOL, false);
    }

    private void HandleStopPush()
    {
        handAnimator.SetBool(IS_PUSHING_BOOL, false);
    }

    private void HandlePull(bool obj)
    {
        if (obj)
        {
            handAnimator.SetTrigger(PULL_ANIMATION_TRIGGER);
            handAnimator.SetBool(IS_PULLING_BOOL, true);
            pullAudioSource.pitch = UnityEngine.Random.Range(1.4f, 1.7f);
            pullAudioSource.Play();
        }
        else
        {
            pullAudioSource.pitch = UnityEngine.Random.Range(1.4f, 1.7f);
            pullAudioSource.Play();
            handAnimator.SetBool(IS_PULLING_BOOL, true);
        }
    }

    private void HandlePush(bool obj)
    {
        if (obj)
        {
            handAnimator.SetTrigger(PUSH_ANIMATION_TRIGGER);
            handAnimator.SetBool(IS_PUSHING_BOOL, true);
            pushAudioSource.pitch = UnityEngine.Random.Range(1.4f, 1.7f);
            pushAudioSource.Play();
        }
        else
        {
            pushAudioSource.pitch = UnityEngine.Random.Range(1.4f, 1.7f);
            pushAudioSource.Play();
            handAnimator.SetBool(IS_PUSHING_BOOL, true);
        }
    }
}
