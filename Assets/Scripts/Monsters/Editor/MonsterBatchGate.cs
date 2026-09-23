using System;
using UnityEditor;

namespace JapaneseDemonHunter.Monsters.Editor
{
    /// <summary>
    /// Batch entry points are triggered from an [InitializeOnLoad] constructor, which runs before
    /// the editor finishes its startup AssetDatabase refresh. Asset reads and writes issued at that
    /// moment are silently deferred, so automated setup must wait until the editor is idle.
    /// </summary>
    internal static class MonsterBatchGate
    {
        public static void RunWhenEditorIsIdle(Action action)
        {
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    return;
                }

                EditorApplication.update -= tick;
                action();
            };

            EditorApplication.update += tick;
        }
    }
}
