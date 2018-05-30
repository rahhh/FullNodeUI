﻿using NBitcoin;
using Newtonsoft.Json;

namespace Xels.Bitcoin.Features.Wallet.Models
{
    public class WalletBuildTransactionModel
    {
        [JsonProperty(PropertyName = "fee")]
        public Money Fee { get; set; }

        [JsonProperty(PropertyName = "hex")]
        public string Hex { get; set; }

        [JsonProperty(PropertyName = "transactionId")]
        public uint256 TransactionId { get; set; }
    }
}
