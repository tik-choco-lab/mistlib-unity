using System;
using NUnit.Framework;

namespace TikChoco.MistLib.Tests
{
    public sealed class PublicContractTests
    {
        /// <summary>
        /// An unconfigured component must still start. The engine treats every configuration key as
        /// optional, so an empty field sends `{}` and the engine's own defaults apply.
        /// </summary>
        [Test]
        public void EmptyOptionsRunOnTheEngineDefaults()
        {
            var options = new MistClientOptions();

            Assert.DoesNotThrow(() => options.Validate());
            Assert.That(options.UsesSignalingUrl, Is.False);
            Assert.That(options.ResolveConfigJson(), Is.EqualTo(MistClientOptions.EngineDefaultsJson));
        }

        [Test]
        public void AnEmptyNodeIdResolvesToAGeneratedUniqueId()
        {
            var options = new MistClientOptions { NodeId = "   " };

            var first = options.ResolveNodeId();
            Assert.That(first, Is.Not.Empty);
            Assert.That(options.ResolveNodeId(), Is.Not.EqualTo(first));
            Assert.That(new MistClientOptions { NodeId = " player-1 " }.ResolveNodeId(), Is.EqualTo("player-1"));
        }

        [Test]
        public void ConfigJsonIsPassedThroughUnchanged()
        {
            var options = new MistClientOptions { ConfigJson = "{\"aoiRange\":20.0}" };

            Assert.That(options.ResolveConfigJson(), Is.EqualTo("{\"aoiRange\":20.0}"));
        }

        /// <summary>
        /// Both inputs select the signaling transport, and the engine would silently let one win.
        /// </summary>
        [Test]
        public void SettingBothSignalingInputsIsRejected()
        {
            var options = new MistClientOptions
            {
                SignalingUrl = "wss://example.invalid/signaling",
                ConfigJson = "{}"
            };

            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [Test]
        public void PackageAndEngineVersionsMatchForTheInitialRelease()
        {
            Assert.That(MistVersion.Package, Is.EqualTo(MistVersion.Engine));
        }

        [TestCase(MistEventType.Raw, 0u)]
        [TestCase(MistEventType.Join, 2u)]
        [TestCase(MistEventType.AllConnectionsLost, 9u)]
        public void EventValuesMatchTheNativeAbi(MistEventType value, uint expected)
        {
            Assert.That((uint)value, Is.EqualTo(expected));
        }

        [TestCase(MistDeliveryMethod.ReliableOrdered, 0u)]
        [TestCase(MistDeliveryMethod.UnreliableOrdered, 1u)]
        [TestCase(MistDeliveryMethod.Unreliable, 2u)]
        public void DeliveryValuesMatchTheNativeAbi(MistDeliveryMethod value, uint expected)
        {
            Assert.That((uint)value, Is.EqualTo(expected));
        }
    }
}
