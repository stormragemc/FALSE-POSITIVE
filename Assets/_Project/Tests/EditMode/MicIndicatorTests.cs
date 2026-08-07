using FalsePositive.UI;
using NUnit.Framework;

namespace FalsePositive.Tests
{
    public sealed class MicIndicatorTests
    {
        [Test]
        public void RmsToMeter_IsMonotonicAcrossVoiceRange()
        {
            float quiet = MicIndicator.RmsToMeter(0.001f, 0.25f);
            float normal = MicIndicator.RmsToMeter(0.03f, 0.25f);
            float loud = MicIndicator.RmsToMeter(0.25f, 0.25f);

            Assert.That(normal, Is.GreaterThan(quiet));
            Assert.That(loud, Is.GreaterThan(normal));
        }

        [TestCase(0f, 0f)]
        [TestCase(0.25f, 1f)]
        [TestCase(1f, 1f)]
        public void RmsToMeter_ClampsToDisplayRange(float rms, float expected)
        {
            Assert.That(MicIndicator.RmsToMeter(rms, 0.25f), Is.EqualTo(expected).Within(0.0001f));
        }
    }
}
