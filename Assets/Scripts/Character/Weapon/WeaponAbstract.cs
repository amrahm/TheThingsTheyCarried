﻿using System;
using static ExtensionMethods.HelperMethods;
using UnityEngine;

public abstract class WeaponAbstract : MonoBehaviour {
    protected const int UpperBodyLayerIndex = 1;

    [NonSerialized] public CharacterMasterAbstract holder;
    protected Animator anim;
    protected MovementAbstract movement;

    /// <summary> Is the weapon swinging </summary>
    protected bool swinging;

    /// <summary> Did the weapon already hit something this swing </summary>
    protected bool hitSomething;

    private WeaponAnimationEventObjects _animEventObjs;
    private Coroutine _fadeCoroutine;

    private void Awake() {
        _animEventObjs = WeaponAnimationEventObjects.Instance;
    }

    public abstract void Attack(float horizontal, float vertical, bool hPressed, bool vPressed);

    /// <summary>
    /// Receives events from weapon animations.
    /// To add events that are only for a special weapon, override this and call this base method in the last else block
    /// </summary>
    /// <param name="e"> The object sent from the animation </param>
    /// <param name="duration"> An optional duration that some events need </param>
    public virtual void ReceiveAnimationEvent(AnimationEventObject e, float duration) {
        if(e == _animEventObjs.swingFadeIn) {
            if(_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = FadeAnimationLayer(this, anim, FadeType.FadeIn, UpperBodyLayerIndex, duration);
        } else if(e == _animEventObjs.swingStart) {
            swinging = true;
        } else if(e == _animEventObjs.swingEnd) {
            swinging = false;
            hitSomething = false;
            if(_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = FadeAnimationLayer(this, anim, FadeType.FadeOut, UpperBodyLayerIndex, duration);
        }
    }
}