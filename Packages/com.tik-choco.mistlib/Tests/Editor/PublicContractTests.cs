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
        public void Package071TargetsEngine071()
        {
            Assert.That(MistVersion.Engine, Is.EqualTo("0.7.1"));
            Assert.That(MistVersion.Package, Is.EqualTo("0.7.1"));
        }

        [Test]
        public void NativeBuildDetailsAreAvailableWithoutInitializingAClient()
        {
            string version;
            string buildInfo;
            try
            {
                version = MistClient.GetNativeVersion();
                buildInfo = MistClient.GetNativeBuildInfoJson();
            }
            catch (PlatformNotSupportedException exception)
            {
                Assert.Ignore(exception.Message);
                return;
            }

            Assert.That(version, Is.EqualTo(MistVersion.Engine));
            Assert.That(buildInfo, Does.Contain("\"version\""));
            Assert.That(buildInfo, Does.Contain(version));
            Assert.That(buildInfo, Does.Contain("\"commit\""));
            Assert.That(buildInfo, Does.Contain("\"target\""));
        }

        [Test]
        public void RoomOperationsRequireInitialization()
        {
            using (var client = new MistClient())
            {
                Assert.Throws<InvalidOperationException>(() => client.LeaveRoom("lobby"));
                Assert.Throws<InvalidOperationException>(() => client.LeaveAllRooms());
            }
        }

        [Test]
        public void RoomOperationsRejectADisposedClient()
        {
            var client = new MistClient();
            client.Dispose();

            Assert.Throws<ObjectDisposedException>(() => client.LeaveRoom("lobby"));
            Assert.Throws<ObjectDisposedException>(() => client.LeaveAllRooms());
        }

        [TestCase(MistEventType.Raw, 0u)]
        [TestCase(MistEventType.Overlay, 1u)]
        [TestCase(MistEventType.PeerConnected, 2u)]
        [TestCase(MistEventType.PeerDisconnected, 3u)]
        [TestCase(MistEventType.Neighbors, 4u)]
        [TestCase(MistEventType.AreaOfInterestEntered, 5u)]
        [TestCase(MistEventType.AreaOfInterestLeft, 6u)]
        [TestCase(MistEventType.NodePositionUpdated, 7u)]
        [TestCase(MistEventType.AreaOfInterestNodes, 8u)]
        [TestCase(MistEventType.AllConnectionsLost, 9u)]
        [TestCase(MistEventType.RoomJoined, 10u)]
        [TestCase(MistEventType.RoomJoinFailed, 11u)]
        [TestCase(MistEventType.RoomLeft, 12u)]
        public void EventValuesMatchTheNativeAbi(MistEventType value, uint expected)
        {
            Assert.That((uint)value, Is.EqualTo(expected));
        }

        [Test]
        public void EventTypesContainOnlyTheUnifiedApplicationCodes()
        {
            Assert.That(Enum.GetNames(typeof(MistEventType)), Is.EqualTo(new[]
            {
                "Raw", "Overlay", "PeerConnected", "PeerDisconnected", "Neighbors",
                "AreaOfInterestEntered", "AreaOfInterestLeft", "NodePositionUpdated",
                "AreaOfInterestNodes", "AllConnectionsLost", "RoomJoined", "RoomJoinFailed", "RoomLeft"
            }));
        }

        [TestCase(MistDeliveryMethod.ReliableOrdered, 0u)]
        [TestCase(MistDeliveryMethod.UnreliableOrdered, 1u)]
        [TestCase(MistDeliveryMethod.Unreliable, 2u)]
        [TestCase(MistDeliveryMethod.ReliableUnordered, 3u)]
        public void DeliveryValuesMatchTheNativeAbi(MistDeliveryMethod value, uint expected)
        {
            Assert.That((uint)value, Is.EqualTo(expected));
        }
    }
}
