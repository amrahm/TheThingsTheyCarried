﻿using System.Collections;
using ExtensionMethods;
using static ExtensionMethods.HelperMethods;
using UnityEngine;
using static AnimationParameters.Weapon;


public abstract class WeaponAbstract : MonoBehaviour {
    private const int UpperBodyLayerIndex = 1;

    /// <summary> How long until an attack is considered held down </summary>
    private const float TapThreshold = 0.1f;

    /// <summary> How long attack buffer lasts </summary>
    private const float BufferTime = 0.3f;

    #region Variables

    protected CharacterMasterAbstract holder;
    private CharacterControlAbstract _ctrl;
    protected Animator anim;
    protected MovementAbstract mvmt;

    /// <summary> Is the weapon swinging </summary>
    protected bool swinging;

    /// <summary> Is an attack started (not necessarily swinging yet) </summary>
    private bool _attacking;

    public bool Blocking { get; private set; }

    /// <summary> what was horizontal attack last frame </summary>
    private int _oldHoriz;

    /// <summary> what was vertical attack last frame </summary>
    private int _oldVert;

    /// <summary> when did the current attack start </summary>
    private float _attackStart;

    /// <summary> when did the attempted buffer attack start? </summary>
    private float _bufferAttackStart;

    private WeaponAnimationEventObjects _animEventObjs;
    private Coroutine _fadeCoroutine;
    private Coroutine _bufferAttackCoroutine;

    #endregion

    private void Awake() {
        _animEventObjs = WeaponAnimationEventObjects.Instance;
    }

    private void Update() {
        if(_ctrl.attackHorizontal != 0 && _ctrl.attackHorizontal != _oldHoriz || // If we have new horizontal input
           _ctrl.attackVertical != 0 && _ctrl.attackVertical != _oldVert) { // or new vertical input
            // Then initiate an attack
            if(!_attacking) {
                StartCoroutine(_InitAttack());
                _attackStart = Time.time;
            } else if(_attacking && (_oldVert == 0 && _oldHoriz == 0 || Time.time - _attackStart > TapThreshold * 2)) {
                // but if we were already attacking and this is an entirely new attack input, or enough time has passed
                // that we know this isn't just them trying to do a diagonal, then start a buffer attack
                if(_bufferAttackCoroutine != null) StopCoroutine(_bufferAttackCoroutine);
                _bufferAttackCoroutine = StartCoroutine(_InitAttack(true));
                _bufferAttackStart = Time.time;
            }
        }
        _oldHoriz = _ctrl.attackHorizontal;
        _oldVert = _ctrl.attackVertical;
    }

    /// <summary> Returns the current directions of attack input </summary>
    private int[] GetAttackDir() => new[] {_ctrl.attackHorizontal, _ctrl.attackVertical};

    /// <summary> Inititates the attack as either a tap or a hold </summary>
    private IEnumerator _InitAttack(bool isBufferAttack = false) {
        //TODO canFlip still being incremented too many times, likely has to do with buffer attack
        IEnumerator WaitTillNotAttacking() {
            yield return new WaitWhile(() => _attacking);
            if(Time.time - _bufferAttackStart < BufferTime) {
                _bufferAttackStart = 0;
                isBufferAttack = false;
            }
        }

        _attacking = true;
        int[] attackInit = GetAttackDir();
        float attackInitTime = Time.time;
        do {
            if(Time.time >= attackInitTime + TapThreshold) {
                if(isBufferAttack) yield return StartCoroutine(WaitTillNotAttacking());
                if(isBufferAttack) yield break; // too long since buffer started
                _attacking = true; // have to do this again in case this was a buffer attack
                if(_ctrl.blockPressed) StartCoroutine(_HoldBlock(attackInit));
                else {
                    AttackHold(attackInit, GetAttackDir());
                    mvmt.cantFlip++;
                    print("ATTACK HOLD INC " + mvmt.cantFlip);
                }
                yield break;
            }
            yield return null;
        } while(_ctrl.attackHorizontal != 0 || _ctrl.attackVertical != 0);

        if(isBufferAttack) yield return StartCoroutine(WaitTillNotAttacking());
        if(isBufferAttack) yield break; // too long since buffer started
        _attacking = true; // have to do this again in case this was a buffer attack
        if(_ctrl.blockPressed) TapBlock(attackInit);
        else {
            AttackTap(attackInit, GetAttackDir());
            mvmt.cantFlip++;
            print("ATTACK TAP INC " + mvmt.cantFlip);
        }
    }

    private void TapBlock(int[] initDir) {
        if(mvmt.FlipInt != initDir[0]) mvmt.Flip();
        anim.SetBool(MeleeBlockingAnim, true);
        anim.SetTrigger(TapBlockForwardAnim);
        mvmt.cantFlip++;
        print("BLOCK TAP INC " + mvmt.cantFlip);
        Blocking = true;
    }

    private IEnumerator _HoldBlock(int[] initDir) {
        if(mvmt.FlipInt != initDir[0]) mvmt.Flip();
        FadeAttackIn(0.15f);
        anim.SetBool(HoldBlockForwardAnim, true);
        anim.SetBool(MeleeBlockingAnim, true);
        mvmt.cantFlip++;
        print("BLOCK HOLD INC " + mvmt.cantFlip);
        Blocking = true;
        yield return new WaitWhile(() => _ctrl.attackHorizontal == initDir[0] && _ctrl.attackVertical == initDir[1]);
        anim.SetBool(HoldBlockForwardAnim, false);
        anim.SetBool(MeleeBlockingAnim, false);
        FadeAttackOut(0.15f);
    }

    protected abstract void AttackTap(int[] initDirection, int[] direction);
    protected abstract void AttackHold(int[] initDirection, int[] direction);

    protected IEnumerator _AttackDash(Vector2 direction, float speed,
                                      float acceleration = 20f, float perpVelCancelSpeed = 1.1f) {
        direction = direction.normalized;
        Vector2 perpVec = Vector3.Cross(direction, Vector3.forward);
        float maxVel = Mathf.Max(Mathf.Sqrt(Vector2.Dot(mvmt.rb.velocity, direction)) + speed, speed);
        bool beforeSwing = !swinging;
        while(beforeSwing) {
            if(swinging) beforeSwing = false;
            yield return null;
        }
        while(swinging) {
            mvmt.rb.gravityScale = 0;
            Vector2 vel = mvmt.rb.velocity;
            Vector2 newVel = vel.SharpInDamp(direction * maxVel, 5).Projected(direction) +
                             vel.Projected(perpVec).SharpInDamp(Vector2.zero, perpVelCancelSpeed);
            if(!float.IsNaN(newVel.x)) mvmt.rb.velocity = newVel;
            maxVel += acceleration * Time.fixedDeltaTime;
            yield return Yields.WaitForFixedUpdate;
        }
        mvmt.rb.gravityScale = 1;
    }

    /// <summary> Call this when you pickup or switch to this weapon </summary>
    /// <param name="newHolder"> The CharacterMasterAbstract of the character that just picked up this weapon </param>
    public virtual void OnEquip(CharacterMasterAbstract newHolder) {
        holder = newHolder;
        _ctrl = holder.control;
        holder.weapon = this;
        anim = holder.gameObject.GetComponent<Animator>();
        mvmt = holder.gameObject.GetComponent<MovementAbstract>();
    }

    /// <summary> Call this when you switch away from this weapon </summary>
    public abstract void OnUnequip();

    /// <summary> Call this when you drop this weapon </summary>
    public void OnDrop(CharacterMasterAbstract newHolder) {
        OnUnequip();
        holder = null;
        _ctrl = null;
        anim = null;
        mvmt = null;
    }

    /// <summary>
    /// Receives events from weapon animations.
    /// To add events that are only for a special weapon, override this and call this base method in the last else block
    /// </summary>
    /// <param name="e"> The object sent from the animation </param>
    /// <param name="duration"> An optional duration that some events need </param>
    public void ReceiveAnimationEvent(AnimationEventObject e, float duration) {
        if(e == _animEventObjs.swingFadeIn) {
            FadeAttackIn(duration);
        } else if(e == _animEventObjs.swingStart) {
            BeginSwing();
        } else if(e == _animEventObjs.swingEnd) {
            EndSwing();
        } else if(e == _animEventObjs.swingFadeOut) {
            FadeAttackOut(duration);
        }
    }

    private void FadeAttackIn(float duration) {
        if(_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = FadeAnimationLayer(this, anim, FadeType.FadeIn, UpperBodyLayerIndex, duration);
    }

    protected void FadeAttackOut(float duration) {
        if(!_attacking) return; //FIXME if another attack starts before animation event
        if(_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = FadeAnimationLayer(this, anim, FadeType.FadeOut, UpperBodyLayerIndex, duration);
        mvmt.cantFlip--;
        print("FADE OUT DEC " + mvmt.cantFlip + "   " + duration);
        anim.SetBool(MeleeBlockingAnim, false);
        _attacking = false;
        Blocking = false;
    }

    protected virtual void BeginSwing() { swinging = true; }

    protected void EndSwing() { swinging = false; }
}