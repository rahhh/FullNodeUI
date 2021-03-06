﻿using System;
using System.Threading.Tasks;
using NBitcoin;
using Xels.Bitcoin.Broadcasting;

namespace Xels.Bitcoin.Interfaces
{
    public interface IBroadcasterManager
    {
        Task BroadcastTransactionAsync(Transaction transaction);

        event EventHandler<TransactionBroadcastEntry> TransactionStateChanged;

        TransactionBroadcastEntry GetTransaction(uint256 transactionHash);

        void AddOrUpdate(Transaction transaction, State state);
    }
}
