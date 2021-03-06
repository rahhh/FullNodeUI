﻿using System;
using NBitcoin;
using Xels.Bitcoin.P2P.Peer;
using Xels.Bitcoin.P2P.Protocol.Payloads;

namespace Xels.Bitcoin.P2P.Protocol.Filters
{
    public class NetworkPeerFiltersCollection : ThreadSafeCollection<INetworkPeerFilter>
    {
        public IDisposable Add(Action<IncomingMessage, Action> onReceiving, Action<INetworkPeer, Payload, Action> onSending = null)
        {
            return base.Add(new ActionFilter(onReceiving, onSending));
        }
    }
}
