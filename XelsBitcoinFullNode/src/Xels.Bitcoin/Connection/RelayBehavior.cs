using System.Threading.Tasks;
using Xels.Bitcoin.P2P.Peer;
using Xels.Bitcoin.P2P.Protocol;
using Xels.Bitcoin.P2P.Protocol.Behaviors;

namespace Xels.Bitcoin.Connection
{
    public class RelayBehavior : NetworkPeerBehavior
    {
        public RelayBehavior()
        {
        }

        public override object Clone()
        {
            return new RelayBehavior();
        }

        protected override void AttachCore()
        {
            this.AttachedPeer.StateChanged.Register(this.OnStateChangedAsync);
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
        }

        private Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            return Task.CompletedTask;
        }

        private Task OnStateChangedAsync(INetworkPeer peer, NetworkPeerState oldState)
        {
            return Task.CompletedTask;
        }

        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
            this.AttachedPeer.StateChanged.Unregister(this.OnStateChangedAsync);
        }
    }
}
