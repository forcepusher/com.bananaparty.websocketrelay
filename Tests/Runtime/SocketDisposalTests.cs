using System;
using System.Threading.Tasks;
using NUnit.Framework;
using BananaParty.WebSocketRelay;

namespace BananaParty.WebSocketRelay.Tests
{
    public class SocketDisposalTests
    {
        [Test]
        public void Dispose_BeforeConnect_DoesNotThrow()
        {
            var socket = new Socket("ws://localhost:8080");
            Assert.DoesNotThrow(() => socket.Dispose());
        }

        [Test]
        public void Dispose_MultipleTimes_DoesNotThrow()
        {
            var socket = new Socket("ws://localhost:8080");
            socket.Connect();
            socket.Dispose();
            Assert.DoesNotThrow(() => socket.Dispose());
        }

        [Test]
        public void Send_AfterDispose_ThrowsInvalidOperationException()
        {
            var socket = new Socket("ws://localhost:8080");
            socket.Connect();
            socket.Dispose();

            // Since IsConnected might be false after dispose, we check if it throws as expected
            // or if it's just not connected.
            // The current implementation of Send checks IsConnected.
            Assert.IsFalse(socket.IsConnected);
            Assert.Throws<InvalidOperationException>(() => socket.Send(new byte[] { 1, 2, 3 }));
        }

        [Test]
        public void ReadPayloadQueue_AfterDispose_ThrowsInvalidOperationException()
        {
            var socket = new Socket("ws://localhost:8080");
            socket.Connect();
            socket.Dispose();

            Assert.Throws<InvalidOperationException>(() => socket.ReadPayloadQueue());
        }
    }
}
