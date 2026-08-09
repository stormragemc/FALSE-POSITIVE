using System.Collections.Generic;
using System.Linq;
using FalsePositive.Core;
using FalsePositive.Dialogue;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FalsePositive.Tests
{
    public sealed class DialogueFillerAssetTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Interrogation.unity";

        [Test]
        public void InterrogationSceneWiresTheTenCanonicalFillerClips()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasAlreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasAlreadyLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                Assert.That(scene.IsValid(), Is.True);
                DialogueManager manager = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<DialogueManager>(true))
                    .SingleOrDefault();
                Assert.That(manager, Is.Not.Null);

                var serializedManager = new SerializedObject(manager);
                SerializedProperty source = serializedManager.FindProperty("fillerSource");
                SerializedProperty clips = serializedManager.FindProperty("fillerClips");
                SerializedProperty config = serializedManager.FindProperty("config");

                Assert.That(source.objectReferenceValue, Is.Not.Null);
                Assert.That(config.objectReferenceValue, Is.Not.Null);
                Assert.That(clips.arraySize, Is.EqualTo(10));

                var names = new HashSet<string>();
                for (int index = 0; index < clips.arraySize; index++)
                {
                    var clip = clips.GetArrayElementAtIndex(index).objectReferenceValue as AudioClip;
                    Assert.That(clip, Is.Not.Null, $"fillerClips[{index}] is not assigned");
                    names.Add(clip.name);
                }

                Assert.That(names.Count, Is.EqualTo(10));
                Assert.That(names.OrderBy(name => name), Is.EqualTo(new[]
                {
                    "SPASSKY-FILLER-001",
                    "SPASSKY-FILLER-003",
                    "SPASSKY-FILLER-004",
                    "SPASSKY-FILLER-005",
                    "SPASSKY-FILLER-006",
                    "SPASSKY-FILLER-007",
                    "SPASSKY-FILLER-009",
                    "SPASSKY-FILLER-010",
                    "SPASSKY-FILLER-013",
                    "SPASSKY-FILLER-023",
                }));

                var interrogationConfig = config.objectReferenceValue as InterrogationConfig;
                Assert.That(interrogationConfig.fillerPlaybackDelaySeconds,
                    Is.EqualTo(0.15f).Within(0.001f));
            }
            finally
            {
                if (!wasAlreadyLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
