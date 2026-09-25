using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[CustomEditor(typeof(ZombiePursuerAI))]
public class ZombiePursuerAIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Prototype setup: reads Idle, Walk, PrepareAttack and Attack from the assigned controller, then creates a private controller and clip copies. Existing controllers/clips are kept.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Create Pursuer Animator From Assigned Controller"))
                CreateController((ZombiePursuerAI)target);
        }
    }

    private static void CreateController(ZombiePursuerAI pursuer)
    {
        Animator animator = pursuer.animator != null ? pursuer.animator : pursuer.GetComponentInChildren<Animator>(true);
        AnimatorController source = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
        if (source == null)
        {
            Debug.LogError("Assign the copied zombie Animator Controller to the child Animator first. This setup expects an AnimatorController, not an AnimatorOverrideController.", pursuer);
            return;
        }

        var sourceClips = new Dictionary<string, AnimationClip>();
        string[] names = { "Idle", "Walk", "PrepareAttack", "Attack", "Flinch", "Hit", "StumbleFall" };
        foreach (string name in names)
        {
            AnimatorState found = FindState(source.layers[0].stateMachine, name);
            if (found != null && found.motion is AnimationClip clip) sourceClips[name] = clip;
        }
        foreach (string required in names.Take(4))
        {
            if (!sourceClips.ContainsKey(required))
            {
                Debug.LogError($"Could not find a {required} state using a single AnimationClip in the controller's first layer. Rename the corresponding state or assign its clip, then retry. No assets were created.", pursuer);
                return;
            }
        }
        AnimationClip sourceAttack = sourceClips["Attack"];
        if (sourceAttack.length < 0.1f)
        {
            Debug.LogError("The Attack clip must be at least 0.1 seconds long for this prototype setup.", pursuer);
            return;
        }

        const string folder = "Assets/Daybreak/PursuerPrototype";
        EnsureFolder(folder);
        AnimatorController controller = null;
        try
        {
            string controllerPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/Pursuer.controller");
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            foreach (string trigger in new[] { "PrepareAttack", "Attack", "CancelAttack", "Flinch", "Hit", "StumbleFall" })
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);

            var clips = new Dictionary<string, AnimationClip>();
            foreach (var pair in sourceClips)
            {
                AnimationClip clip = UnityEngine.Object.Instantiate(pair.Value);
                clip.name = "Pursuer_" + pair.Key;
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = pair.Key == "Idle" || pair.Key == "Walk";
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + clip.name + ".anim");
                AssetDatabase.CreateAsset(clip, path);
                clips[pair.Key] = clip;
            }

            AnimationClip attack = clips["Attack"];
            AnimationEvent oldContact = attack.events.FirstOrDefault(e => e.functionName == "NormalAttackHit" && e.time > 0f && e.time < attack.length);
            float contact = oldContact != null ? oldContact.time : attack.length * 0.35f;
            // The existing pose at contact stays intact. State playback speed
            // brings it to about 0.25 s after Attack begins; the wind-up has its
            // own visible phase. These are prototype settings, not Hunt data.
            float attackSpeed = Mathf.Clamp(contact / 0.25f, 0.2f, 4f);
            float stepStart = Mathf.Min(contact * 0.1f, 1f / Mathf.Max(1f, attack.frameRate));
            float complete = Mathf.Min(attack.length * 0.99f, contact + 0.4f * attackSpeed);
            complete = Mathf.Max(contact, complete);
            var events = attack.events.Where(e => e.functionName != "PursuerStepStart" &&
                e.functionName != "NormalAttackHit" && e.functionName != "AttackComplete").ToList();
            events.Add(new AnimationEvent { functionName = "PursuerStepStart", time = stepStart, floatParameter = contact / attack.length });
            events.Add(new AnimationEvent { functionName = "NormalAttackHit", time = contact });
            events.Add(new AnimationEvent { functionName = "AttackComplete", time = complete });
            attack.events = events.OrderBy(e => e.time).ToArray();
            EditorUtility.SetDirty(attack);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>();
            int index = 0;
            foreach (var pair in clips)
            {
                AnimatorState state = machine.AddState(pair.Key, new Vector3(240f + (index % 3) * 260f, 80f + (index / 3) * 130f, 0f));
                state.motion = pair.Value;
                state.writeDefaultValues = false;
                if (pair.Key == "Attack") state.speed = attackSpeed;
                states[pair.Key] = state;
                index++;
            }
            machine.defaultState = states["Idle"];

            // Prioritise death/reactions over attack requests. Only an attack
            // trigger enters Prepare/Attack; MoveSpeed cannot abort a swing.
            if (states.ContainsKey("StumbleFall")) AnyTransition(machine, states["StumbleFall"], "StumbleFall");
            if (states.ContainsKey("Hit")) AnyTransition(machine, states["Hit"], "Hit");
            if (states.ContainsKey("Flinch")) AnyTransition(machine, states["Flinch"], "Flinch");
            AnyTransition(machine, states["Walk"], "CancelAttack");
            AnyTransition(machine, states["Attack"], "Attack");
            AnyTransition(machine, states["PrepareAttack"], "PrepareAttack");

            AnimatorStateTransition walk = states["Idle"].AddTransition(states["Walk"]);
            Configure(walk);
            walk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "MoveSpeed");
            AnimatorStateTransition idle = states["Walk"].AddTransition(states["Idle"]);
            Configure(idle);
            idle.AddCondition(AnimatorConditionMode.Less, 0.1f, "MoveSpeed");
            foreach (string name in new[] { "Attack", "Hit", "Flinch" })
            {
                if (!states.ContainsKey(name)) continue;
                AnimatorStateTransition finish = states[name].AddTransition(states["Walk"]);
                Configure(finish);
                finish.hasExitTime = true;
                finish.exitTime = 1f;
            }

            AssetDatabase.SaveAssets();
            Undo.RecordObject(animator, "Assign Pursuer Animator");
            Undo.RecordObject(pursuer, "Assign Pursuer References");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            pursuer.animator = animator;
            if (animator.GetComponent<ZombieAnimationEvents>() == null)
                Undo.AddComponent<ZombieAnimationEvents>(animator.gameObject);
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(pursuer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            PrefabUtility.RecordPrefabInstancePropertyModifications(pursuer);
            EditorGUIUtility.PingObject(controller);
            Debug.Log($"Pursuer Animator created. Contact is approximately {contact / attackSpeed:F2} s into Attack; movement uses the clip's event interval. Verify the contact pose in Play Mode. Assets: {controllerPath}", pursuer);
            if (oldContact == null)
                Debug.LogWarning("No existing NormalAttackHit event was found. A temporary contact at 35% of the copied Attack clip was used. Inspect that pose and move NormalAttackHit and the PursuerStepStart float parameter together before judging hit timing.", pursuer);
            if (!states.ContainsKey("StumbleFall"))
                Debug.LogWarning("StumbleFall was not found; EnemyHealth's existing ragdoll fallback still handles death.", pursuer);
        }
        catch (Exception exception)
        {
            // Do not delete source assets. Newly generated assets are retained
            // for inspection if assignment had already succeeded.
            Debug.LogException(exception, pursuer);
            Debug.LogError("Pursuer setup did not finish. The original source controller/clips were not edited. Check the Console before retrying.", pursuer);
        }
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        foreach (ChildAnimatorState child in machine.states)
            if (string.Equals(child.state.name, name, StringComparison.OrdinalIgnoreCase)) return child.state;
        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
        {
            AnimatorState found = FindState(child.stateMachine, name);
            if (found != null) return found;
        }
        return null;
    }

    private static void AnyTransition(AnimatorStateMachine machine, AnimatorState state, string trigger)
    {
        AnimatorStateTransition transition = machine.AddAnyStateTransition(state);
        Configure(transition);
        transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    private static void Configure(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.05f;
        transition.canTransitionToSelf = false;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string parent = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = parent + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, parts[i]);
            parent = next;
        }
    }
}
