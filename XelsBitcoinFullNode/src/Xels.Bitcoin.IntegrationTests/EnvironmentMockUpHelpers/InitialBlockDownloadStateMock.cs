﻿using System;
using NBitcoin;
using Xels.Bitcoin.Base;
using Xels.Bitcoin.Configuration;
using Xels.Bitcoin.Features.Consensus;
using Xels.Bitcoin.Interfaces;
using Xels.Bitcoin.Utilities;

namespace Xels.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers
{
    public class InitialBlockDownloadStateMock : IInitialBlockDownloadState
    {
        /// <summary>Time until IBD state can be checked.</summary>
        private DateTime lockIbdUntil;

        /// <summary>A cached result of the IBD method.</summary>
        private bool blockDownloadState;

        /// <summary>A provider of the date and time.</summary>
        protected readonly IDateTimeProvider dateTimeProvider;

        private readonly InitialBlockDownloadState innerBlockDownloadState;

        public InitialBlockDownloadStateMock(IChainState chainState, Network network, NodeSettings nodeSettings,
            ICheckpoints checkpoints)
        {
            this.lockIbdUntil = DateTime.MinValue;
            this.dateTimeProvider = DateTimeProvider.Default;

            this.innerBlockDownloadState = new InitialBlockDownloadState(chainState, network, nodeSettings, checkpoints);
        }

        public bool IsInitialBlockDownload()
        {
            if (this.lockIbdUntil >= this.dateTimeProvider.GetUtcNow())
                return this.blockDownloadState;

            return this.innerBlockDownloadState.IsInitialBlockDownload();
        }

        /// <summary>
        /// Sets last IBD status update time and result.
        /// </summary>
        /// <param name="blockDownloadState">New value for the IBD status, <c>true</c> means the node is considered in IBD.</param>
        /// <param name="lockIbdUntil">Time until IBD state won't be changed.</param>
        public void SetIsInitialBlockDownload(bool blockDownloadState, DateTime lockIbdUntil)
        {
            this.lockIbdUntil = lockIbdUntil;
            this.blockDownloadState = blockDownloadState;
        }
    }
}
