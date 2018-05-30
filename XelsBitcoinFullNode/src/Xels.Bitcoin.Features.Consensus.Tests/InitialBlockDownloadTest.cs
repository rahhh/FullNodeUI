﻿using NBitcoin;
using Xels.Bitcoin.Base;
using Xels.Bitcoin.Configuration;
using Xels.Bitcoin.Configuration.Logging;
using Xels.Bitcoin.Utilities;
using Xunit;

namespace Xels.Bitcoin.Features.Consensus.Tests
{
    public class InitialBlockDownloadTest
    {
        private readonly ConsensusSettings consensusSettings;
        private readonly Checkpoints checkpoints;
        private readonly NodeSettings nodeSettings;
        private readonly ChainState chainState;
        private readonly Network network;

        public InitialBlockDownloadTest()
        {
            this.network = Network.Main;
            this.consensusSettings = new ConsensusSettings().Load(NodeSettings.Default());
            this.checkpoints = new Checkpoints(this.network, this.consensusSettings);
            this.nodeSettings = new NodeSettings(this.network);
            this.chainState = new ChainState(new InvalidBlockHashStore(DateTimeProvider.Default));
        }

        [Fact]
        public void NotInIBDIfChainStateIsNull()
        {
            var blockDownloadState = new InitialBlockDownloadState(null, this.network, this.nodeSettings, this.checkpoints);
            Assert.False(blockDownloadState.IsInitialBlockDownload());
        }

        [Fact]
        public void InIBDIfChainTipIsNull()
        {
            this.chainState.ConsensusTip = null;
            var blockDownloadState = new InitialBlockDownloadState(this.chainState, this.network, this.nodeSettings, this.checkpoints);
            Assert.True(blockDownloadState.IsInitialBlockDownload());
        }

        [Fact]
        public void InIBDIfBehindCheckpoint()
        {
            this.chainState.ConsensusTip = new ChainedBlock(new BlockHeader(), uint256.Zero, 1000);
            var blockDownloadState = new InitialBlockDownloadState(this.chainState, this.network, this.nodeSettings, this.checkpoints);
            Assert.True(blockDownloadState.IsInitialBlockDownload());
        }

        [Fact]
        public void InIBDIfChainWorkIsLessThanMinimum()
        {
            this.chainState.ConsensusTip = new ChainedBlock(new BlockHeader(), uint256.Zero, this.checkpoints.GetLastCheckpointHeight() + 1);
            var blockDownloadState = new InitialBlockDownloadState(this.chainState, this.network, this.nodeSettings, this.checkpoints);
            Assert.True(blockDownloadState.IsInitialBlockDownload());
        }
    }
}
