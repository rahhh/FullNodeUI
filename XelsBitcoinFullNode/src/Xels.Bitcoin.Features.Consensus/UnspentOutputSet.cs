﻿using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Xels.Bitcoin.Features.Consensus.CoinViews;
using Xels.Bitcoin.Utilities;

namespace Xels.Bitcoin.Features.Consensus
{
    public class UnspentOutputSet
    {
        private Dictionary<uint256, UnspentOutputs> unspents;

        public TxOut GetOutputFor(TxIn txIn)
        {
            var unspent = this.unspents.TryGet(txIn.PrevOut.Hash);
            if (unspent == null)
                return null;

            return unspent.TryGetOutput(txIn.PrevOut.N);
        }

        public bool HaveInputs(Transaction tx)
        {
            return tx.Inputs.All(txin => this.GetOutputFor(txin) != null);
        }

        public UnspentOutputs AccessCoins(uint256 uint256)
        {
            return this.unspents.TryGet(uint256);
        }

        public Money GetValueIn(Transaction tx)
        {
            return tx.Inputs.Select(txin => this.GetOutputFor(txin).Value).Sum();
        }

        /// <summary>
        /// Adds transaction's outputs to unspent coins list and removes transaction's inputs from it.
        /// </summary>
        /// <param name="transcation">Transaction which inputs and outputs are used for updating unspent coins list.</param>
        /// <param name="height">Height of a block that contains target transaction.</param>
        public void Update(Transaction transcation, int height)
        {
            if (!transcation.IsCoinBase)
            {
                foreach (var input in transcation.Inputs)
                {
                    var c = this.AccessCoins(input.PrevOut.Hash);
                    c.Spend(input.PrevOut.N);
                }
            }

            this.unspents.AddOrReplace(transcation.GetHash(), new UnspentOutputs((uint)height, transcation));
        }

        public void SetCoins(UnspentOutputs[] coins)
        {
            this.unspents = new Dictionary<uint256, UnspentOutputs>(coins.Length);
           // UnspentOutputs uo = new UnspentOutputs(uint256.Parse("0xcdad8a555b8d574e31d66e11841ce2ffa76074c00e4db8afe1f4bcfc79e520bf"), new NBitcoin.BitcoinCore.Coins {  });
            foreach (UnspentOutputs coin in coins)
            {
                if (coin != null)
                    this.unspents.Add(coin.TransactionId, coin);
            }
        }

        public void TrySetCoins(UnspentOutputs[] coins)
        {
            this.unspents = new Dictionary<uint256, UnspentOutputs>(coins.Length);
            foreach (UnspentOutputs coin in coins)
            {
                if (coin != null)
                    this.unspents.TryAdd(coin.TransactionId, coin);
            }
        }

        public IEnumerable<UnspentOutputs> GetCoins(CoinView utxo)
        {
            return this.unspents.Select(u => u.Value).ToList();
        }
    }
}
