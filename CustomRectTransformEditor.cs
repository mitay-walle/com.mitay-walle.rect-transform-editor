using System;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Plugins.UI.Editor
{
    [CustomEditor(typeof(RectTransform), true), CanEditMultipleObjects]
    public class CustomRectTransformEditor : UnityEditor.Editor
    {
        private static PropertyInfo _rectDrivenObject = typeof(RectTransform).GetProperty("drivenByObject"
            , BindingFlags.NonPublic | BindingFlags.Instance);
        private UnityEditor.Editor editorInstance;
        private static Type nativeEditor;
        private MethodInfo onSceneGui;

        private Rect _rect = new Rect(45, 65, 45, 18);
        private Rect _rect2 = new Rect(70, 25, 20, 18);
        private Rect _rect3 = new Rect(70, 45, 20, 18);
        private Rect _rect4 = new Rect(0, 65, 45, 18);

        private void OnEnable()
        {
            if (targets.Length == 0 || targets[0] == null) return;

            Initialize();
            if (nativeEditor == null) return;
            if (editorInstance == null) return;

            try
            {
                if (editorInstance.targets.Length > 0 && editorInstance.targets[0] != null)
                {
                    nativeEditor.GetMethod("OnEnable",
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.Invoke(editorInstance, null);
                }
            }
            catch
            {
            }

            onSceneGui = nativeEditor.GetMethod("OnSceneGUI",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private void Initialize()
        {
            if (nativeEditor == null)
            {
                nativeEditor = Assembly.GetAssembly(typeof(UnityEditor.Editor))
                    .GetType("UnityEditor.RectTransformEditor");
            }

            if (editorInstance)
            {
                CreateCachedEditor(targets, nativeEditor, ref editorInstance);
            }
            else
            {
                editorInstance = CreateEditor(targets, nativeEditor);
            }
        }

        public override void OnInspectorGUI()
        {
            editorInstance.OnInspectorGUI();

            bool needMoveY = NeedMoveY();

            Rect rect = _rect;
            rect.y += needMoveY ? 20 : 0;

            // Code here
            if (GUI.Button(rect, "Snap"))
            {
                foreach (Object targ in targets)
                {
                    if (targ is RectTransform rectTr)
                    {
                        Undo.RecordObject(targ, "Snap to parent");
                        SnapToParent(rectTr);
                        EditorUtility.SetDirty(targ);
                    }
                }
            }

            rect = _rect4;
            rect.y += needMoveY ? 20 : 0;

            if (GUI.Button(rect, "New"))
            {
                foreach (Object targ in targets)
                {
                    if (targ is RectTransform rectTr)
                    {
                        CreateEmptyParentRect(rectTr);
                    }
                }
            }

            if (targets.Length > 1)
            {
                GUI.enabled = false;
            }

            rect = _rect2;
            rect.y += needMoveY ? 20 : 0;

            if (GUI.Button(rect, "C"))
            {
                ComponentUtility.CopyComponent(target as RectTransform);
            }

            GUI.enabled = true;

            rect = _rect3;
            rect.y += needMoveY ? 20 : 0;

            if (GUI.Button(rect, "P"))
            {
                foreach (Object targ in targets)
                {
                    Undo.RecordObject(targ, "Paste");
                    ComponentUtility.PasteComponentValues(target as RectTransform);
                    EditorUtility.SetDirty(targ);
                }
            }
        }

        private void OnSceneGUI()
        {
            onSceneGui.Invoke(editorInstance, null);
        }

        private void OnDisable()
        {
            if (editorInstance) DestroyImmediate(editorInstance);
        }

        private static void SnapToParent(RectTransform rect)
        {
            rect.pivot = new Vector2(.5f, .5f);
            rect.localScale = Vector3.one;
            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            rect.anchoredPosition = Vector2.zero;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
        }

        public static void CreateEmptyParentRect(RectTransform rect)
        {
            GameObject go = new GameObject("Create Empty Parent");

            ComponentUtility.CopyComponent(rect);
            ComponentUtility.PasteComponentAsNew(go);

            Undo.RecordObject(rect, "Create Empty, Reparent");

            Undo.RegisterCreatedObjectUndo(go, "Create Empty Parent");

            RectTransform rect2 = go.transform as RectTransform;

            PlaceSameAs(go.transform, rect, true, true, true);
            ComponentUtility.PasteComponentValues(rect2);
            Undo.SetTransformParent(rect, go.transform, "Create Empty, Reparent");
            SnapToParent(rect);

            EditorUtility.SetDirty(rect);

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        private static void PlaceSameAs(Transform target, Transform source, bool copyName = false, bool undo = false,
            bool setDirty = false)
        {
            if (!source) return;

            if (undo && Application.isPlaying)
            {
                Undo.RecordObject(target, "PlaceSameAs");
                if (copyName) Undo.RecordObject(target.gameObject, "PlaceSameAs");
            }

            VectorArray matrix = new VectorArray(source, false);
            matrix.Apply(target);
            if (undo)
            {
                Undo.SetTransformParent(target, source.parent, "PlaceSameAs");
            }
            else
            {
                target.SetParent(source.parent);
            }

            target.SetSiblingIndex(source.GetSiblingIndex());

            if (copyName) target.name = source.name;

            if (setDirty && Application.isPlaying)
            {
                EditorUtility.SetDirty(target);
                if (copyName) EditorUtility.SetDirty(target.gameObject);
            }
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private bool NeedMoveY()
        {
            bool value = false;
            foreach (RectTransform target in targets)
            {
                if (NeedMoveY(target))
                {
                    value = true;
                    break;
                }
            }

            return value;
        }

        private bool NeedMoveY(RectTransform target) => target.drivenByObject != null;
    }

    [Serializable]
    public struct VectorArray
    {
        [SerializeField] public bool local;
        [SerializeField] public Vector3 Pos;
        [SerializeField] public Quaternion Rot;
        [SerializeField] public Vector3 Scale;

        public VectorArray(Transform tr, bool local)
        {
            this.local = local;
            if (!tr)
            {
                Pos = default;
                Rot = default;
                Scale = default;
            }
            else
            {
                Scale = local ? tr.localScale : tr.lossyScale;

                if (local)
                {
                    Pos = tr.localPosition;
                    Rot = tr.localRotation;
                }
                else
                {
                    Pos = tr.position;
                    Rot = tr.rotation;
                }
            }
        }

        public void Apply(Transform tr)
        {
            if (local)
            {
                tr.localPosition = Pos;
                tr.localRotation = Rot;
            }
            else
            {
                tr.position = Pos;
                tr.rotation = Rot;
            }

            tr.localScale = Scale;
#if UNITY_EDITOR
            if (!Application.isPlaying) EditorUtility.SetDirty(tr);
#endif
        }
    }
}
