using System;
using System.IO;
using UnityEngine;

namespace FalsePositive.Core
{
    /// <summary>
    /// Fills in <see cref="InterrogationConfig.backendBaseUrl"/> and
    /// <see cref="InterrogationConfig.backendClientKey"/> from a gitignored
    /// StreamingAssets file at runtime, so the shipped
    /// InterrogationConfig.asset can stay committed with both fields empty
    /// (Sidecar/tests/test_unity_contract.py requires exactly that — no
    /// secret may land in git).
    ///
    /// See Assets/StreamingAssets/backend.local.example.json for the shape.
    /// Copy it to Assets/StreamingAssets/backend.local.json (same folder,
    /// gitignored) and fill in the client key to reach the hosted backend.
    /// A missing file is the normal state for a fresh clone — no error, no
    /// log spam, just the existing local-sidecar fallback.
    /// </summary>
    public static class BackendRuntimeOverride
    {
        private const string OverrideFileName = "backend.local.json";

        [Serializable]
        private sealed class OverrideDto
        {
            public string backendBaseUrl;
            public string backendClientKey;
        }

        /// <summary>
        /// Returns <paramref name="source"/> unchanged if no override file is
        /// present. Otherwise returns a runtime-only clone with the two
        /// fields overwritten — never mutates <paramref name="source"/>
        /// itself, so the on-disk asset is never at risk of picking up a
        /// secret via an accidental Editor save.
        /// </summary>
        public static InterrogationConfig Apply(InterrogationConfig source)
        {
            if (source == null || !TryLoad(out OverrideDto over))
            {
                return source;
            }

            InterrogationConfig clone = UnityEngine.Object.Instantiate(source);
            if (!string.IsNullOrWhiteSpace(over.backendBaseUrl))
            {
                clone.backendBaseUrl = over.backendBaseUrl.Trim();
            }
            if (!string.IsNullOrEmpty(over.backendClientKey))
            {
                clone.backendClientKey = over.backendClientKey;
            }
            return clone;
        }

        private static bool TryLoad(out OverrideDto result)
        {
            result = null;
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, OverrideFileName);
                if (!File.Exists(path))
                {
                    return false;
                }

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                OverrideDto parsed = JsonUtility.FromJson<OverrideDto>(json);
                if (parsed == null)
                {
                    return false;
                }

                result = parsed;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BackendRuntimeOverride] Failed to read {OverrideFileName}: {e.Message}");
                return false;
            }
        }
    }
}
