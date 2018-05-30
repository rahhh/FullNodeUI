using System;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Xels.Bitcoin.Base;
using Xels.Bitcoin.Configuration;

namespace Xels.Bitcoin
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ActionDescription : Attribute
    {
        public string Description { get; private set; }

        public ActionDescription(string description)
        {
            this.Description = description;
        }
    }

    public abstract class FeatureController : Controller
    {
        protected IFullNode FullNode;

        protected NodeSettings Settings;

        protected Network Network;

        protected ChainBase Chain;

        protected IChainState ChainState;

        protected Connection.IConnectionManager ConnectionManager;

        public FeatureController(
            IFullNode fullNode = null,
            NodeSettings nodeSettings = null,
            Network network = null,
            ConcurrentChain chain = null,
            IChainState chainState = null,
            Connection.IConnectionManager connectionManager = null)
        {
            this.FullNode = fullNode;
            this.Settings = nodeSettings;
            this.Network = network;
            this.Chain = chain;
            this.ChainState = chainState;
            this.ConnectionManager = connectionManager;
        }
    }
}