﻿using System;
using System.Collections.Generic;
using System.Linq;
using ExtensionMethods;
using UnityEditor;
using UnityEngine;
using static PlayerPhysics;

public class PlayerPhysics : MonoBehaviour {
    public List<BodyPartClass> bodyParts;
    public Dictionary<Collider2D, BodyPartClass> collToPart = new Dictionary<Collider2D, BodyPartClass>();

    private PlayerMovement _playerMovement; //Reference to PlayerMovement script

    /// <summary> Reference to Parts script, which contains all of the player's body parts </summary>
    private Parts _parts;


    private void Awake() {
        //References
        _playerMovement = GetComponent<PlayerMovement>();
        _parts = GetComponent<Parts>();

        foreach(var part in bodyParts) part.Initialize(collToPart, bodyParts);
    }

    private void Update() {
        foreach(var part in bodyParts) {
            part.Right = part.bodyPart.transform.right;
            part.FacingRight = _playerMovement.facingRight;
        }
    }

    private void FixedUpdate() {
        RotateTo(_parts.torso, _parts.torsoTarget);
        RotateTo(_parts.head, _parts.headTarget);
        RotateTo(_parts.upperArmR, _parts.upperArmRTarget);
        RotateTo(_parts.lowerArmR, _parts.lowerArmRTarget);
        RotateTo(_parts.handR, _parts.handRTarget);
        RotateTo(_parts.upperArmL, _parts.upperArmLTarget);
        RotateTo(_parts.lowerArmL, _parts.lowerArmLTarget);
        RotateTo(_parts.handL, _parts.handLTarget);
        RotateTo(_parts.thighR, _parts.thighRTarget);
        RotateTo(_parts.shinR, _parts.shinRTarget);
        RotateTo(_parts.footR, _parts.footRTarget);
        RotateTo(_parts.thighL, _parts.thighLTarget);
        RotateTo(_parts.shinL, _parts.shinLTarget);
        RotateTo(_parts.footL, _parts.footLTarget);
        RotateTo(_parts.hips, _parts.hipsTarget);

        foreach(var part in collToPart.Values) {
            part.HitRotation();
            part.HandleTouching();
            part.PostRight = part.bodyPart.transform.right;
        }
    }

    /// <summary> Rotates the non-animated skeleton to the animated skeleton </summary>
    /// <param name="obj">part from the non-animated skeleton</param>
    /// <param name="target">part from the animated skeleton</param>
    private void RotateTo(GameObject obj, GameObject target) {
#if DEBUG || UNITY_EDITOR
        //Log any errors, since this shouldn't happen
        if(!_parts.partsToLPositions.ContainsKey(obj))
            Debug.LogError($"Trying to rotate {obj.name}, but it wasn't found in{nameof(Parts)}.{nameof(Parts.partsToLPositions)}");
#endif

        //Reset the local positions cause sometimes they get moved
        obj.transform.localPosition = _parts.partsToLPositions[obj];

        //Match the animation rotation
        obj.transform.rotation = target.transform.rotation;
    }

    [Serializable]
    public class BodyPartClass {
        #region Variables

        [Tooltip("The body part to rotate/control")]
        public GameObject bodyPart;

        [Tooltip("The parent body part of this body part. Can be null.")]
        public GameObject parentPart;

        [Tooltip("A list of all of the objects that contain colliders for this body part")]
        public List<GameObject> colliderObjects = new List<GameObject>();

        [Tooltip("How intensely this part reacts to impacts")]
        public float partWeakness = 65;

        [Tooltip("Is this body part a leg, i.e. should it handle touching the floor differently")]
        public bool isLeg;

        [Tooltip("A list of all objects that should bend left when crouching, along with an amount from 0 to 1 that they should bend")]
        public List<GameObject> bendLeft = new List<GameObject>();

        [Tooltip("The amount that the corresponding part should rotate from 0 to 1")]
        public List<float> bendLeftAmounts = new List<float>();

        [Tooltip("A list of all objects that should bend right when crouching, along with an amount from 0 to 1 that they should bend")]
        public List<GameObject> bendRight = new List<GameObject>();

        [Tooltip("The amount that the corresponding part should rotate from 0 to 1")]
        public List<float> bendRightAmounts = new List<float>();


        /// <summary> The parent body part class of this body part. Can be null. </summary>
        private BodyPartClass _parent;

        /// <summary> How far up the length of the body part the collision was </summary>
        private float _upDown;

        /// <summary> A vector from the base position of this body part to the point of the collision </summary>
        private Vector3 _positionVector;

        /// <summary> The normal vector of the collision </summary>
        private Vector2 _collisionNormal;

        /// <summary> How much this part is rotating beyond normal </summary>
        private float _rotAmount;

        /// <summary> How much torque is actively being added to rotate this part </summary>
        private float _torqueAmount;

        /// <summary> A list of all other legs that should crouch with this one") </summary>
        private IEnumerable<BodyPartClass> _linkedLegs;

        /// <summary> If this is a leg, how much crouch is being added from an impact </summary>
        private float _crouchPlus;

        /// <summary> If this is a leg, how much should it crouch </summary>
        private float _crouchAmount;

        /// <summary> Should the hit rotation code run? </summary>
        private bool _shouldHitRot;

        /// <summary> Is the body part currently touching something? </summary>
        private bool _isTouching;

        /// <summary> The rightward direction of this bodypart before rotating </summary>
        public Vector3 Right { private get; set; }

        /// <summary> The rightward direction of this bodypart after rotating </summary>
        public Vector3 PostRight { private get; set; }

        /// <summary> Is the player facing right? </summary>
        public bool FacingRight { private get; set; }

        /// <summary> Reference to the rigidbody </summary>
        private Rigidbody2D _rb;

        /// <summary> Vector from the base to the tip of this part </summary>
        private Vector3 _topVector;

        /// <summary> Adds all of the colliderObjects to a handy dictionary named collToPart.
        /// Also determines the length of this body part by looking at all of these colliders, thenceby setting _topVector </summary>
        /// <param name="collToPart">The dictionary to set up</param>
        /// <param name="bodyParts">A list of all BodyPart classes</param>
        public void Initialize(IDictionary<Collider2D, BodyPartClass> collToPart, List<BodyPartClass> bodyParts) {
            if(parentPart != null) _parent = bodyParts.First(part => part.bodyPart == parentPart);
            if(isLeg) _linkedLegs = bodyParts.Where(part => part.isLeg);
            _rb = bodyPart.GetComponentInParent<Rigidbody2D>();
            Vector3 objPos = bodyPart.transform.position;
            Vector3 farPoint = objPos;
            Collider2D farColl = bodyPart.GetComponent<Collider2D>();
            foreach(GameObject co in colliderObjects) {
                Collider2D[] colliders = co.GetComponents<Collider2D>();
                foreach(Collider2D collider in colliders) {
                    if(Vector3.Distance(collider.bounds.center, objPos) >= Vector3.Distance(farPoint, objPos)) {
                        farPoint = collider.bounds.center;
                        farColl = collider;
                    }
                    collToPart.Add(collider, this);
                }
            }
            farPoint = Vector3.Distance(farPoint + farColl.bounds.extents, objPos) < Vector3.Distance(farPoint - farColl.bounds.extents, objPos)
                           ? bodyPart.transform.InverseTransformPoint(farPoint - farColl.bounds.extents)
                           : bodyPart.transform.InverseTransformPoint(farPoint + farColl.bounds.extents);
            _topVector = new Vector3(farPoint.x, 0);
        }

        #endregion

        /// <summary> Calculate how much rotation should be added on collision </summary>
        /// <param name="point">Point of contact</param>
        /// <param name="direction">Direction of contact</param>
        /// <param name="impulse">Impluse of the collision</param>
        public void HitCalc(Vector3 point, Vector2 direction, Vector2 impulse) {
            _shouldHitRot = true; //enable HitRot() to apply rotation
            _isTouching = true; //enable IsTouching() for continuous touching
            _collisionNormal = direction;
            _positionVector = point - bodyPart.transform.position; //A vector to the position of the hit

            Vector3 toTop = bodyPart.transform.TransformPoint(_topVector) - bodyPart.transform.position; //A vector to the top of this part
            _upDown = Mathf.Clamp(Vector3.Dot(toTop, _positionVector) / Vector3.SqrMagnitude(toTop), -1, 1); //Clamped in case of errors

            //All of the impulse in the direction of the collision normal
            Vector2 forceVectorPre = Vector2.Dot(impulse, direction) * direction * _upDown;
            //If it's a leg, only take the horizontal component
            Vector2 forceVector = isLeg ? Vector2.Dot(forceVectorPre, Vector2.right) * Vector2.right : forceVectorPre;

            if(isLeg) { //Add crouching using the vertical component for legs
                float verticalForce = (forceVectorPre - forceVector).y;
                if(verticalForce / _rb.mass > 0.2f) { //Min threshold so this isn't constantly active
                    foreach(var leg in _linkedLegs) leg._crouchPlus += verticalForce;
                }
            }

            //Add the magnitude of this force to torqueAmount, which will make the part rotate. The cross product gives us the proper direction.
            _torqueAmount += forceVector.magnitude * Mathf.Sign(Vector3.Cross(_positionVector, forceVector).z);

            //Transfer force that was removed because of a low upDown (+ a bit more) at the hinge of this part in the direction of the impulse to the parent
            _parent?.HitCalc(bodyPart.transform.position, impulse.normalized, (1.5f - _upDown) * impulse);
        }

        /// <summary> Rotates the body part, dispersing the collision torque over time to return to the resting position </summary>
        public void HitRotation() {
            if(isLeg) CrouchRotation();
            if(!_shouldHitRot) return;

            _rotAmount += _torqueAmount * Time.fixedDeltaTime; //Build up a rotation based on the amount of torque from the collision
            bodyPart.transform.Rotate(Vector3.forward, partWeakness * _rotAmount / 2, Space.Self); //Rotate the part _rotAmount past where it is animated

            _torqueAmount -= _torqueAmount * 3 * Time.fixedDeltaTime; //Over time, reduce the torque added from the collision
            _rotAmount = Extensions.SharpInDamp(_rotAmount, 7 * _rotAmount / 8, 0.8f, Time.fixedDeltaTime); //and return the body part back to rest

            _shouldHitRot = Mathf.Abs(_rotAmount) * partWeakness >= 0.01f; //If the rotation is small enough, stop calling this code
        }

        /// <summary> Handles Contracting multi-part legs when they hit the ground </summary>
        private void CrouchRotation() {
            if(_crouchAmount < 0.1f && _crouchPlus < 0.1f) return;

            _crouchAmount = Extensions.SharpInDamp(_crouchAmount, _crouchPlus, 1f, Time.fixedDeltaTime); //Quickly move towards crouchAmount
            for(int i = 0; i < bendRight.Count; i++)
                bendRight[i].transform.Rotate(Vector3.forward, (FacingRight ? -1 : 1) * _crouchAmount * bendRightAmounts[i], Space.Self);
            for(int i = 0; i < bendLeft.Count; i++)
                bendLeft[i].transform.Rotate(Vector3.forward, (FacingRight ? 1 : -1) * _crouchAmount * bendLeftAmounts[i], Space.Self);

            _crouchPlus -= _crouchPlus * 1 * Time.fixedDeltaTime; //Over time, reduce the crouch from impact
        }

        /// <summary> Adjusts the rotation of this part when rotating into something that it's touching </summary>
        public void HandleTouching() {
            //TODO Something with computepenetration
            if(!_isTouching || !((FacingRight ? -1 : 1) * Vector3.Dot(_collisionNormal, (PostRight - Right).normalized) > 0.1f)) return;
            //TODO Is massMult needed here?
            float torquePlus = (FacingRight ? 1 : -1) * -2 * 40 * _positionVector.magnitude * Vector3.Angle(PostRight, Right) / 5 *
                               Mathf.Sign(Vector3.Cross(_collisionNormal, PostRight).z) * _upDown * (isLeg ? Vector2.Dot(_collisionNormal, Vector2.right) : 1);
            _torqueAmount += torquePlus * Time.fixedDeltaTime;
            _shouldHitRot = true;
            _isTouching = false;
        }
    }

    /// <summary> Passes info from collision events to the BodyPartClass HitCalc method </summary>
    /// <param name="collInfo">The collision info from the collision event</param>
    private void CollisionHandler(Collision2D collInfo) {
        if(collInfo.gameObject.GetComponent<Rigidbody2D>()) {
            foreach(ContactPoint2D c in collInfo.contacts) {
                if(collToPart.ContainsKey(c.otherCollider)) {
                    BodyPartClass part = collToPart[c.otherCollider];
                    Vector2 force = float.IsNaN(c.normalImpulse) ? collInfo.relativeVelocity : c.normalImpulse / Time.fixedDeltaTime * c.normal / 1000;
                    part.HitCalc(c.point, c.normal, force);
                }
            }
        } else {
            foreach(ContactPoint2D c in collInfo.contacts) {
                if(collToPart.ContainsKey(c.otherCollider)) {
                    BodyPartClass part = collToPart[c.otherCollider];
                    Vector2 force = float.IsNaN(c.normalImpulse) ? collInfo.relativeVelocity : c.normalImpulse / Time.fixedDeltaTime * c.normal / 1000;
                    part.HitCalc(c.point, c.normal, force);
                }
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collInfo) {
        CollisionHandler(collInfo);
    }

    private void OnCollisionStay2D(Collision2D collInfo) {
        CollisionHandler(collInfo);
    }
}

[CustomEditor(typeof(PlayerPhysics))]
public class PlayerPhysicsInspector : Editor {
    private PlayerPhysics _t;
    private SerializedObject _getTarget;
    private SerializedProperty _bodyParts;

    private void OnEnable() {
        _t = (PlayerPhysics) target;
        _getTarget = new SerializedObject(_t);
        _bodyParts = _getTarget.FindProperty(nameof(PlayerPhysics.bodyParts)); // Find the List in our script and create a refrence of it
    }

    public override void OnInspectorGUI() {
        //Show the script field
        serializedObject.Update();
        SerializedProperty prop = serializedObject.FindProperty("m_Script");
        GUI.enabled = false;
        EditorGUILayout.PropertyField(prop, true);
        GUI.enabled = true;
        serializedObject.ApplyModifiedProperties();

        //Update our list
        _getTarget.Update();

        //Display our list to the inspector window
        for(int i = 0; i < _bodyParts.arraySize; i++) {
            SerializedProperty bodyPartClassRef = _bodyParts.GetArrayElementAtIndex(i);
            SerializedProperty bodyPart = bodyPartClassRef.FindPropertyRelative(nameof(BodyPartClass.bodyPart));
            SerializedProperty parentPart = bodyPartClassRef.FindPropertyRelative(nameof(BodyPartClass.parentPart));
            SerializedProperty colliderObjects = bodyPartClassRef.FindPropertyRelative(nameof(BodyPartClass.colliderObjects));
            SerializedProperty partWeakness = bodyPartClassRef.FindPropertyRelative(nameof(BodyPartClass.partWeakness));
            SerializedProperty isLeg = bodyPartClassRef.FindPropertyRelative(nameof(BodyPartClass.isLeg));
            SerializedProperty bendLeft = bodyPartClassRef.FindPropertyRelative(nameof(BodyPartClass.bendLeft));
            SerializedProperty bendLeftAmounts = bodyPartClassRef.FindPropertyRelative(nameof(BodyPartClass.bendLeftAmounts));
            SerializedProperty bendRight = bodyPartClassRef.FindPropertyRelative(nameof(BodyPartClass.bendRight));
            SerializedProperty bendRightAmounts = bodyPartClassRef.FindPropertyRelative(nameof(BodyPartClass.bendRightAmounts));

            string partName = bodyPart.objectReferenceValue == null ? "Part " + i : bodyPart.objectReferenceValue.name;

            bodyPart.isExpanded = EditorGUILayout.Foldout(bodyPart.isExpanded, partName, true);
            if(bodyPart.isExpanded) {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(bodyPart);
                EditorGUILayout.PropertyField(parentPart);
                PlusMinusGameObjectList(colliderObjects, i);
                EditorGUILayout.PropertyField(partWeakness);
                EditorGUILayout.PropertyField(isLeg);
                if(isLeg.boolValue) {
                    EditorGUI.indentLevel++;
                    PlusMinusGameObjectList(bendLeft, i, bendLeftAmounts);
                    PlusMinusGameObjectList(bendRight, i, bendRightAmounts);
                    EditorGUI.indentLevel--;
                }

                //Add a delete button
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if(GUILayout.Button("   Delete " + partName + " Body Part   ")) {
                    _bodyParts.DeleteArrayElementAtIndex(i);
                }
                GUILayout.Space(10);
                GUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }
        }

        EditorGUILayout.Space();
        if(GUILayout.Button("Add New Body Part")) {
            _t.bodyParts.Add(new BodyPartClass());
            _getTarget.Update();
            _bodyParts.GetArrayElementAtIndex(_bodyParts.arraySize - 1).FindPropertyRelative(nameof(BodyPartClass.bodyPart)).isExpanded = true;
        }

        //Apply the changes to our list
        _getTarget.ApplyModifiedProperties();
    }

    /// <summary> Displays a collapsible list with a plus next to the list name and a minus next to each entry </summary>
    /// <param name="list">The list to display</param>
    /// <param name="bodyPartClassIndex">Index in the bodyParts list where this BodyPartClass is</param>
    /// <param name="list2">A second list to display right next to the first</param>
    private void PlusMinusGameObjectList(SerializedProperty list, int bodyPartClassIndex, SerializedProperty list2 = null) {
        GUILayout.BeginHorizontal();
        list.isExpanded = EditorGUILayout.Foldout(list.isExpanded,
            new GUIContent(list.displayName, Extensions.GetTooltip(_t.bodyParts[bodyPartClassIndex].GetType().GetField(list.name), true)), true);
        if(list.isExpanded) {
            if(list2 != null) {
                while(list2.arraySize < list.arraySize) {
                    list2.InsertArrayElementAtIndex(list2.arraySize);
                    list2.GetArrayElementAtIndex(list2.arraySize - 1).floatValue = 1;
                }
            }
            if(GUILayout.Button("", GUIStyle.none, GUILayout.ExpandWidth(true))) list.isExpanded = !list.isExpanded;
            if(GUILayout.Button("   +   ", GUILayout.MaxWidth(60), GUILayout.MaxHeight(15))) {
                list.InsertArrayElementAtIndex(list.arraySize);
                list2?.InsertArrayElementAtIndex(list2.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = null;
                if(list2 != null) list2.GetArrayElementAtIndex(list2.arraySize - 1).floatValue = 1;
            }
            GUILayout.EndHorizontal();

            EditorGUI.indentLevel++;
            EditorGUI.indentLevel++;
            if(list.arraySize == 0) {
                list.InsertArrayElementAtIndex(0);
                list2?.InsertArrayElementAtIndex(0);
                if(list2 != null) list2.GetArrayElementAtIndex(0).floatValue = 1;
            }

            for(int a = 0; a < list.arraySize; a++) {
                GUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(list.GetArrayElementAtIndex(a), new GUIContent(""));
                if(list2 != null) {
                    GUILayout.Space(-45);
                    EditorGUILayout.PropertyField(list2.GetArrayElementAtIndex(a), new GUIContent(""), GUILayout.MaxWidth(100));
                    GUILayout.Space(5);
                }

                if(GUILayout.Button("  -  ", GUILayout.MaxWidth(40), GUILayout.MaxHeight(15))) {
                    if(list.GetArrayElementAtIndex(a).objectReferenceValue != null)
                        list.DeleteArrayElementAtIndex(a); //Delete the value first
                    list.DeleteArrayElementAtIndex(a); //Then delete the whole entry
                    list2?.DeleteArrayElementAtIndex(a);
                }
                GUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
            EditorGUI.indentLevel--;
        } else {
            GUILayout.EndHorizontal();
        }
    }
}