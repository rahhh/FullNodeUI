using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.BitcoinCore;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using Xels.Bitcoin.Utilities;
using Xels.Bitcoin.Utilities.JsonConverters;

namespace Xels.Bitcoin.Features.Wallet
{
    /// <summary>
    /// A wallet.
    /// </summary>
    public class Wallet
    {
        /// <summary>
        /// Initializes a new instance of the wallet.
        /// </summary>
        public Wallet()
        {
            this.AccountsRoot = new List<AccountRoot>();
        }

        /// <summary>
        /// The name of this wallet.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// The seed for this wallet, password encrypted.
        /// </summary>
        [JsonProperty(PropertyName = "encryptedSeed")]
        public string EncryptedSeed { get; set; }

        /// <summary>
        /// The chain code.
        /// </summary>
        [JsonProperty(PropertyName = "chainCode")]
        [JsonConverter(typeof(ByteArrayConverter))]
        public byte[] ChainCode { get; set; }

        /// <summary>
        /// Gets or sets the merkle path.
        /// </summary>
        [JsonProperty(PropertyName = "blockLocator", ItemConverterType = typeof(UInt256JsonConverter))]
        public ICollection<uint256> BlockLocator { get; set; }

        /// <summary>
        /// The network this wallet is for.
        /// </summary>
        [JsonProperty(PropertyName = "network")]
        [JsonConverter(typeof(NetworkConverter))]
        public Network Network { get; set; }

        /// <summary>
        /// The time this wallet was created.
        /// </summary>
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }

        /// <summary>
        /// The root of the accounts tree.
        /// </summary>
        [JsonProperty(PropertyName = "accountsRoot")]
        public ICollection<AccountRoot> AccountsRoot { get; set; }

        /// <summary>
        /// Gets the accounts the wallet has for this type of coin.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns>The accounts in the wallet corresponding to this type of coin.</returns>
        public IEnumerable<HdAccount> GetAccountsByCoinType(CoinType coinType)
        {
            return this.AccountsRoot.Where(a => a.CoinType == coinType).SelectMany(a => a.Accounts);
        }

        /// <summary>
        /// Gets an account from the wallet's accounts.
        /// </summary>
        /// <param name="accountName">The name of the account to retrieve.</param>
        /// <param name="coinType">The type of the coin this account is for.</param>
        /// <returns>The requested account.</returns>
        public HdAccount GetAccountByCoinType(string accountName, CoinType coinType)
        {
            AccountRoot accountRoot = this.AccountsRoot.SingleOrDefault(a => a.CoinType == coinType);
            return accountRoot?.GetAccountByName(accountName);
        }

        /// <summary>
        /// Update the last block synced height and hash in the wallet.
        /// </summary>
        /// <param name="coinType">The type of the coin this account is for.</param>
        /// <param name="block">The block whose details are used to update the wallet.</param>
        public void SetLastBlockDetailsByCoinType(CoinType coinType, ChainedBlock block)
        {
            AccountRoot accountRoot = this.AccountsRoot.SingleOrDefault(a => a.CoinType == coinType);

            if (accountRoot == null) return;

            accountRoot.LastBlockSyncedHeight = block.Height;
            accountRoot.LastBlockSyncedHash = block.HashBlock;
        }

        /// <summary>
        /// Gets all the transactions by coin type.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns></returns>
        public IEnumerable<TransactionData> GetAllTransactionsByCoinType(CoinType coinType)
        {
            var accounts = this.GetAccountsByCoinType(coinType).ToList();

            foreach (TransactionData txData in accounts.SelectMany(x => x.ExternalAddresses).SelectMany(x => x.Transactions))
            {
                yield return txData;
            }

            foreach (TransactionData txData in accounts.SelectMany(x => x.InternalAddresses).SelectMany(x => x.Transactions))
            {
                yield return txData;
            }
        }

        /// <summary>
        /// Gets all the pub keys contained in this wallet.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns></returns>
        public IEnumerable<Script> GetAllPubKeysByCoinType(CoinType coinType)
        {
            var accounts = this.GetAccountsByCoinType(coinType).ToList();

            foreach (Script script in accounts.SelectMany(x => x.ExternalAddresses).Select(x => x.ScriptPubKey))
            {
                yield return script;
            }

            foreach (Script script in accounts.SelectMany(x => x.InternalAddresses).Select(x => x.ScriptPubKey))
            {
                yield return script;
            }
        }

        /// <summary>
        /// Gets all the addresses contained in this wallet.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns>A list of all the addresses contained in this wallet.</returns>
        public IEnumerable<HdAddress> GetAllAddressesByCoinType(CoinType coinType)
        {
            var accounts = this.GetAccountsByCoinType(coinType).ToList();

            List<HdAddress> allAddresses = new List<HdAddress>();
            foreach (HdAccount account in accounts)
            {
                allAddresses.AddRange(account.GetCombinedAddresses());
            }
            return allAddresses;
        }

        /// <summary>
        /// Adds an account to the current wallet.
        /// </summary>
        /// <remarks>
        /// The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains at least one transaction.
        /// </remarks>
        /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki"/>
        /// <param name="password">The password used to decrypt the wallet's <see cref="EncryptedSeed"/>.</param>
        /// <param name="coinType">The type of coin this account is for.</param>
        /// <param name="accountCreationTime">Creation time of the account to be created.</param>
        /// <returns>A new HD account.</returns>
        public HdAccount AddNewAccount(string password, CoinType coinType, DateTimeOffset accountCreationTime)
        {
            Guard.NotEmpty(password, nameof(password));

            var accountRoot = this.AccountsRoot.Single(a => a.CoinType == coinType);
            return accountRoot.AddNewAccount(password, this.EncryptedSeed, this.ChainCode, this.Network, accountCreationTime);
        }

        /// <summary>
        /// Gets the first account that contains no transaction.
        /// </summary>
        /// <returns>An unused account.</returns>
        public HdAccount GetFirstUnusedAccount(CoinType coinType)
        {
            // Get the accounts root for this type of coin.
            var accountsRoot = this.AccountsRoot.Single(a => a.CoinType == coinType);

            if (accountsRoot.Accounts.Any())
            {
                // Get an unused account.
                var firstUnusedAccount = accountsRoot.GetFirstUnusedAccount();
                if (firstUnusedAccount != null)
                {
                    return firstUnusedAccount;
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether the wallet contains the specified address.
        /// </summary>
        /// <param name="address">The address to check.</param>
        /// <returns>A value indicating whether the wallet contains the specified address.</returns>
        public bool ContainsAddress(HdAddress address)
        {
            if (!this.AccountsRoot.Any(r => r.Accounts.Any(
                a => a.ExternalAddresses.Any(i => i.Address == address.Address) ||
                     a.InternalAddresses.Any(i => i.Address == address.Address))))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the extended private key for the given address.
        /// </summary>
        /// <param name="password">The password used to encrypt/decrypt sensitive info.</param>
        /// <param name="address">The address to get the private key for.</param>
        /// <returns>The extended private key.</returns>
        public ISecret GetExtendedPrivateKeyForAddress(string password, HdAddress address)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotNull(address, nameof(address));

            // Check if the wallet contains the address.
            if (!this.ContainsAddress(address))
            {
                throw new WalletException("Address not found on wallet.");
            }

            // get extended private key
            Key privateKey = HdOperations.DecryptSeed(this.EncryptedSeed, password, this.Network);
            return HdOperations.GetExtendedPrivateKey(privateKey, this.ChainCode, address.HdPath, this.Network);
        }

        /// <summary>
        /// Lists all spendable transactions from all accounts in the wallet.
        /// </summary>
        /// <param name="coinType">Type of the coin to get transactions from.</param>
        /// <param name="currentChainHeight">Height of the current chain, used in calculating the number of confirmations.</param>
        /// <param name="confirmations">The number of confirmations required to consider a transaction spendable.</param>
        /// <returns>A collection of spendable outputs.</returns>
        public IEnumerable<UnspentOutputReference> GetAllSpendableTransactions(CoinType coinType, int currentChainHeight, int confirmations = 0)
        {
            IEnumerable<HdAccount> accounts = this.GetAccountsByCoinType(coinType);

            return accounts
                .SelectMany(x => x.GetSpendableTransactions(currentChainHeight, confirmations));
        }
    }

    /// <summary>
    /// The root for the accounts for any type of coins.
    /// </summary>
    public class AccountRoot
    {
        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        public AccountRoot()
        {
            this.Accounts = new List<HdAccount>();
        }

        /// <summary>
        /// The type of coin, Bitcoin or Xels.
        /// </summary>
        [JsonProperty(PropertyName = "coinType")]
        public CoinType CoinType { get; set; }

        /// <summary>
        /// The height of the last block that was synced.
        /// </summary>
        [JsonProperty(PropertyName = "lastBlockSyncedHeight", NullValueHandling = NullValueHandling.Ignore)]
        public int? LastBlockSyncedHeight { get; set; }

        /// <summary>
        /// The hash of the last block that was synced.
        /// </summary>
        [JsonProperty(PropertyName = "lastBlockSyncedHash", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 LastBlockSyncedHash { get; set; }

        /// <summary>
        /// The accounts used in the wallet.
        /// </summary>
        [JsonProperty(PropertyName = "accounts")]
        public ICollection<HdAccount> Accounts { get; set; }

        /// <summary>
        /// Gets the first account that contains no transaction.
        /// </summary>
        /// <returns>An unused account</returns>
        public HdAccount GetFirstUnusedAccount()
        {
            if (this.Accounts == null)
                return null;

            var unusedAccounts = this.Accounts.Where(acc => !acc.ExternalAddresses.Any() && !acc.InternalAddresses.Any()).ToList();
            if (!unusedAccounts.Any())
                return null;

            // gets the unused account with the lowest index
            var index = unusedAccounts.Min(a => a.Index);
            return unusedAccounts.Single(a => a.Index == index);
        }

        /// <summary>
        /// Gets the account matching the name passed as a parameter.
        /// </summary>
        /// <param name="accountName">The name of the account to get.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        public HdAccount GetAccountByName(string accountName)
        {
            if (this.Accounts == null)
                throw new WalletException($"No account with the name {accountName} could be found.");

            // get the account
            HdAccount account = this.Accounts.SingleOrDefault(a => a.Name == accountName);
            if (account == null)
                throw new WalletException($"No account with the name {accountName} could be found.");

            return account;
        }

        /// <summary>
        /// Adds an account to the current account root.
        /// </summary>
        /// <remarks>The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains transactions.
        /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki"/></remarks>
        /// <param name="password">The password used to decrypt the wallet's encrypted seed.</param>
        /// <param name="encryptedSeed">The encrypted private key for this wallet.</param>
        /// <param name="chainCode">The chain code for this wallet.</param>
        /// <param name="network">The network for which this account will be created.</param>
        /// <param name="accountCreationTime">Creation time of the account to be created.</param>
        /// <returns>A new HD account.</returns>
        public HdAccount AddNewAccount(string password, string encryptedSeed, byte[] chainCode, Network network, DateTimeOffset accountCreationTime)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(encryptedSeed, nameof(encryptedSeed));
            Guard.NotNull(chainCode, nameof(chainCode));

            // Get the current collection of accounts.
            var accounts = this.Accounts.ToList();

            int newAccountIndex = 0;
            if (accounts.Any())
            {
                newAccountIndex = accounts.Max(a => a.Index) + 1;
            }

            // Get the extended pub key used to generate addresses for this account.
            string accountHdPath = HdOperations.GetAccountHdPath((int)this.CoinType, newAccountIndex);
            Key privateKey = HdOperations.DecryptSeed(encryptedSeed, password, network);
            ExtPubKey accountExtPubKey = HdOperations.GetExtendedPublicKey(privateKey, chainCode, accountHdPath);

            var newAccount = new HdAccount
            {
                Index = newAccountIndex,
                ExtendedPubKey = accountExtPubKey.ToString(network),
                ExternalAddresses = new List<HdAddress>(),
                InternalAddresses = new List<HdAddress>(),
                Name = $"account {newAccountIndex}",
                HdPath = accountHdPath,
                CreationTime = accountCreationTime
            };

            accounts.Add(newAccount);
            this.Accounts = accounts;

            return newAccount;
        }
    }

    /// <summary>
    /// An HD account's details.
    /// </summary>
    public class HdAccount
    {
        public HdAccount()
        {
            this.ExternalAddresses = new List<HdAddress>();
            this.InternalAddresses = new List<HdAddress>();
        }

        /// <summary>
        /// The index of the account.
        /// </summary>
        /// <remarks>
        /// According to BIP44, an account at index (i) can only be created when the account
        /// at index (i - 1) contains transactions.
        /// </remarks>
        [JsonProperty(PropertyName = "index")]
        public int Index { get; set; }

        /// <summary>
        /// The name of this account.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// A path to the account as defined in BIP44.
        /// </summary>
        [JsonProperty(PropertyName = "hdPath")]
        public string HdPath { get; set; }

        /// <summary>
        /// An extended pub key used to generate addresses.
        /// </summary>
        [JsonProperty(PropertyName = "extPubKey")]
        public string ExtendedPubKey { get; set; }

        /// <summary>
        /// Gets or sets the creation time.
        /// </summary>
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }

        /// <summary>
        /// The list of external addresses, typically used for receiving money.
        /// </summary>
        [JsonProperty(PropertyName = "externalAddresses")]
        public ICollection<HdAddress> ExternalAddresses { get; set; }

        /// <summary>
        /// The list of internal addresses, typically used to receive change.
        /// </summary>
        [JsonProperty(PropertyName = "internalAddresses")]
        public ICollection<HdAddress> InternalAddresses { get; set; }

        /// <summary>
        /// Gets the type of coin this account is for.
        /// </summary>
        /// <returns>A <see cref="CoinType"/>.</returns>
        public CoinType GetCoinType()
        {
            return (CoinType)HdOperations.GetCoinType(this.HdPath);
        }

        /// <summary>
        /// Gets the first receiving address that contains no transaction.
        /// </summary>
        /// <returns>An unused address</returns>
        public HdAddress GetFirstUnusedReceivingAddress()
        {
            return this.GetFirstUnusedAddress(false);
        }

        /// <summary>
        /// Gets the first change address that contains no transaction.
        /// </summary>
        /// <returns>An unused address</returns>
        public HdAddress GetFirstUnusedChangeAddress()
        {
            return this.GetFirstUnusedAddress(true);
        }

        /// <summary>
        /// Gets the first receiving address that contains no transaction.
        /// </summary>
        /// <returns>An unused address</returns>
        private HdAddress GetFirstUnusedAddress(bool isChange)
        {
            IEnumerable<HdAddress> addresses = isChange ? this.InternalAddresses : this.ExternalAddresses;
            if (addresses == null)
                return null;

            var unusedAddresses = addresses.Where(acc => !acc.Transactions.Any()).ToList();
            if (!unusedAddresses.Any())
            {
                return null;
            }

            // gets the unused address with the lowest index
            var index = unusedAddresses.Min(a => a.Index);
            return unusedAddresses.Single(a => a.Index == index);
        }

        /// <summary>
        /// Gets the last address that contains transactions.
        /// </summary>
        /// <param name="isChange">Whether the address is a change (internal) address or receiving (external) address.</param>
        /// <returns></returns>
        public HdAddress GetLastUsedAddress(bool isChange)
        {
            IEnumerable<HdAddress> addresses = isChange ? this.InternalAddresses : this.ExternalAddresses;
            if (addresses == null)
                return null;

            var usedAddresses = addresses.Where(acc => acc.Transactions.Any()).ToList();
            if (!usedAddresses.Any())
            {
                return null;
            }

            // gets the used address with the highest index
            var index = usedAddresses.Max(a => a.Index);
            return usedAddresses.Single(a => a.Index == index);
        }

        /// <summary>
        /// Gets a collection of transactions by id.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns></returns>
        public IEnumerable<TransactionData> GetTransactionsById(uint256 id)
        {
            Guard.NotNull(id, nameof(id));

            var addresses = this.GetCombinedAddresses();
            return addresses.Where(r => r.Transactions != null).SelectMany(a => a.Transactions.Where(t => t.Id == id));
        }

        /// <summary>
        /// Gets a collection of transactions with spendable outputs.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<TransactionData> GetSpendableTransactions()
        {
            var addresses = this.GetCombinedAddresses();
            return addresses.Where(r => r.Transactions != null).SelectMany(a => a.Transactions.Where(t => t.IsSpendable()));
        }

        /// <summary>
        /// Get the accounts total spendable value for both confirmed and unconfirmed UTXO.
        /// </summary>
        public (Money ConfirmedAmount, Money UnConfirmedAmount) GetSpendableAmount()
        {
            var allTransactions = this.ExternalAddresses.SelectMany(a => a.Transactions)
                .Concat(this.InternalAddresses.SelectMany(i => i.Transactions)).ToList();

            var confirmed = allTransactions.Sum(t => t.SpendableAmount(true));
            var total = allTransactions.Sum(t => t.SpendableAmount(false));

            return (confirmed, total - confirmed);
        }

        /// <summary>
        /// Finds the addresses in which a transaction is contained.
        /// </summary>
        /// <remarks>
        /// Returns a collection because a transaction can be contained in a change address as well as in a receive address (as a spend).
        /// </remarks>
        /// <param name="predicate">A predicate by which to filter the transactions.</param>
        /// <returns></returns>
        public IEnumerable<HdAddress> FindAddressesForTransaction(Func<TransactionData, bool> predicate)
        {
            Guard.NotNull(predicate, nameof(predicate));

            var addresses = this.GetCombinedAddresses();
            return addresses.Where(t => t.Transactions != null).Where(a => a.Transactions.Any(predicate));
        }

        /// <summary>
        /// Return both the external and internal (change) address from an account.
        /// </summary>
        /// <returns>All addresses that belong to this account.</returns>
        public IEnumerable<HdAddress> GetCombinedAddresses()
        {
            IEnumerable<HdAddress> addresses = new List<HdAddress>();
            if (this.ExternalAddresses != null)
            {
                addresses = this.ExternalAddresses;
            }

            if (this.InternalAddresses != null)
            {
                addresses = addresses.Concat(this.InternalAddresses);
            }

            return addresses;
        }

        /// <summary>
        /// Creates a number of additional addresses in the current account.
        /// </summary>
        /// <remarks>
        /// The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains at least one transaction.
        /// </remarks>
        /// <param name="network">The network these addresses will be for.</param>
        /// <param name="addressesQuantity">The number of addresses to create.</param>
        /// <param name="isChange">Whether the addresses added are change (internal) addresses or receiving (external) addresses.</param>
        /// <returns>The created addresses.</returns>
        public IEnumerable<HdAddress> CreateAddresses(Network network, int addressesQuantity, bool isChange = false)
        {
            var addresses = isChange ? this.InternalAddresses : this.ExternalAddresses;

            // Get the index of the last address.
            int firstNewAddressIndex = 0;
            if (addresses.Any())
            {
                firstNewAddressIndex = addresses.Max(add => add.Index) + 1;
            }

            List<HdAddress> addressesCreated = new List<HdAddress>();
            for (int i = firstNewAddressIndex; i < firstNewAddressIndex + addressesQuantity; i++)
            {
                // Generate a new address.
                PubKey pubkey = HdOperations.GeneratePublicKey(this.ExtendedPubKey, i, isChange);
                BitcoinPubKeyAddress address = pubkey.GetAddress(network);

                // Add the new address details to the list of addresses.
                HdAddress newAddress = new HdAddress
                {
                    Index = i,
                    HdPath = HdOperations.CreateHdPath((int) this.GetCoinType(), this.Index, i, isChange),
                    ScriptPubKey = address.ScriptPubKey,
                    Pubkey = pubkey.ScriptPubKey,
                    Address = address.ToString(),
                    Transactions = new List<TransactionData>()
                };

                ////////////////////// Neo: add premine transaction

                //////-------------------------------------------
                //if (isChange)
                //{
                //    Block genesis = network.GetGenesis();

                //    var newTransactionData = new TransactionData
                //    {
                //        Amount = genesis.Transactions[0].Outputs[0].Value,
                //        IsCoinStake = true,
                //        BlockHeight = 0,
                //        BlockHash = genesis.GetHash(),
                //        Id = genesis.Transactions[0].GetHash(),
                //        CreationTime = DateTimeOffset.FromUnixTimeSeconds(genesis.Transactions[0].Time),
                //        Index = genesis.Transactions[0].Outputs.IndexOf(genesis.Transactions[0].Outputs[0]),
                //        ScriptPubKey = genesis.Transactions[0].Outputs[0].ScriptPubKey,
                //        Hex = genesis.Transactions[0].ToHex(),
                //        IsPropagated = true,
                //    };
                //    newAddress.Transactions.Add(newTransactionData);

                //   var newTransactionData2 = new TransactionData
                //    {
                //        Amount = genesis.Transactions[1].Outputs[0].Value,
                //        IsCoinStake = true,
                //        BlockHeight = 0,
                //        BlockHash = genesis.GetHash(),
                //        Id = genesis.Transactions[1].GetHash(),
                //        CreationTime = DateTimeOffset.FromUnixTimeSeconds(genesis.Transactions[1].Time),
                //        Index = genesis.Transactions[1].Outputs.IndexOf(genesis.Transactions[1].Outputs[0]),
                //        ScriptPubKey = genesis.Transactions[1].Outputs[0].ScriptPubKey,
                //        Hex = genesis.Transactions[1].ToHex(),
                //        IsPropagated = true,
                //    };
                //    newAddress.Transactions.Add(newTransactionData2);
                //}
                //////////--------------------------

                //////-------------------------------------------
                //if (isChange)
                //{
                //    Transaction txNew2 = new Transaction();
                //    txNew2.Version = 1;
                //    txNew2.Time = 1529946948;
                //    txNew2.AddInput(new TxIn());
                //    txNew2.AddOutput(new TxOut(Money.Coins(500000), new Script()));

                //    var newTransactionData = new TransactionData
                //    {
                //        Amount = Money.Coins(500000),
                //        IsCoinStake = true,
                //        BlockHeight = 0,
                //        BlockHash = uint256.Parse("0x03126313e262980525034da1152a6708a7b4cd6802433d4cb02d93e2f93dfc43"), //"000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f",
                //        Id = uint256.Parse("0xa2dfe19b6b9a6fb357250b475ed136db76284f7aa7d35dae0e85bb446e37b274"), //txNew.GetHash(),
                //        CreationTime = DateTimeOffset.FromUnixTimeSeconds(txNew2.Time),
                //        Index = txNew2.Outputs.IndexOf(txNew2.Outputs[0]),
                //        ScriptPubKey = txNew2.Outputs[0].ScriptPubKey,
                //        Hex = txNew2.ToHex(),
                //        IsPropagated = true,
                //    };
                //    newAddress.Transactions.Add(newTransactionData);

                //    Transaction txNew3 = new Transaction();
                //    txNew3.Version = 1;
                //    txNew3.Time = 1529946948;
                //    txNew3.AddInput(new TxIn());
                //    txNew3.AddOutput(new TxOut(Money.Coins(600000), new Script()));

                //    var newTransactionData2 = new TransactionData
                //    {
                //        Amount = Money.Coins(600000),
                //        IsCoinStake = true,
                //        BlockHeight = 0,
                //        BlockHash = uint256.Parse("0x03126313e262980525034da1152a6708a7b4cd6802433d4cb02d93e2f93dfc43"), //"000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f",
                //        Id = uint256.Parse("0x4d0499c437775048e44294ad50e43e96307891f054d719a442663de312016e63"), //txNew.GetHash(),
                //        CreationTime = DateTimeOffset.FromUnixTimeSeconds(txNew3.Time),
                //        Index = txNew3.Outputs.IndexOf(txNew3.Outputs[0]),
                //        ScriptPubKey = txNew3.Outputs[0].ScriptPubKey,
                //        Hex = txNew3.ToHex(),
                //        IsPropagated = true,
                //    };
                //    newAddress.Transactions.Add(newTransactionData2);
                //}
                //////////--------------------------

                ////////////-------------------------------------------
                //if (isChange)
                //{
                //    Transaction txNew2 = new Transaction();
                //    txNew2.Version = 1;
                //    txNew2.Time = 1529946948;

                //    txNew2.AddInput(new TxIn());
                //    txNew2.Inputs[0].PrevOut.Hash = uint256.Parse("0x572488fd787186c6ee3c44af5db233e72e904055099ce17372c466e7881d6f57");
                //    txNew2.Inputs[0].PrevOut.N = 1;
                //    txNew2.Inputs[0].ScriptSig = new Script();
                //    txNew2.AddOutput(new TxOut(Money.Coins(500000), newAddress.ScriptPubKey));

                //    var newTransactionData = new TransactionData
                //    {
                //        Amount = Money.Coins(500000),
                //        IsCoinStake = true,
                //        BlockHeight = 0,
                //        BlockHash = uint256.Parse("0x6c6e81c407f5da7bb43eae31d48e15273dd941765f191ca3732ef8b0d42cd880"), //"000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f",
                //        Id = uint256.Parse("0x572488fd787186c6ee3c44af5db233e72e904055099ce17372c466e7881d6f57"), //txNew.GetHash(),
                //        CreationTime = DateTimeOffset.FromUnixTimeSeconds(txNew2.Time),
                //        Index = txNew2.Outputs.IndexOf(txNew2.Outputs[0]),
                //        ScriptPubKey = txNew2.Outputs[0].ScriptPubKey,
                //        Hex = txNew2.ToHex(),
                //        IsPropagated = true,
                //    };
                //    newAddress.Transactions.Add(newTransactionData);

                //    Transaction txNew3 = new Transaction();
                //    txNew3.Version = 1;
                //    txNew3.Time = 1529946948;

                //    txNew3.AddInput(new TxIn());
                //    txNew3.Inputs[0].PrevOut.Hash = uint256.Parse("0x7769431d731f5a4f9e5fc64b6034ceb29274b26e1ff0d649d62155438cbe5c96");
                //    txNew3.Inputs[0].PrevOut.N = 1;
                //    txNew3.Inputs[0].ScriptSig = new Script();
                //    txNew3.AddOutput(new TxOut(Money.Coins(600000), newAddress.ScriptPubKey));

                //    var newTransactionData2 = new TransactionData
                //    {
                //        Amount = Money.Coins(600000),
                //        IsCoinStake = true,
                //        BlockHeight = 0,
                //        BlockHash = uint256.Parse("0x6c6e81c407f5da7bb43eae31d48e15273dd941765f191ca3732ef8b0d42cd880"), //"000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f",
                //        Id = uint256.Parse("0x7769431d731f5a4f9e5fc64b6034ceb29274b26e1ff0d649d62155438cbe5c96"), //txNew.GetHash(),
                //        CreationTime = DateTimeOffset.FromUnixTimeSeconds(txNew3.Time),
                //        Index = txNew3.Outputs.IndexOf(txNew3.Outputs[0]),
                //        ScriptPubKey = txNew3.Outputs[0].ScriptPubKey,
                //        Hex = txNew3.ToHex(),
                //        IsPropagated = true,
                //    };
                //    newAddress.Transactions.Add(newTransactionData2);
                //}
                ////////////////--------------------------


                ///////////////////////////////////////////
                //DateTime startTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                //TimeSpan currTime = DateTime.Now - startTime;
                //uint time_t = Convert.ToUInt32(Math.Abs(currTime.TotalSeconds));


                //txNew.AddInput(TxIn.CreateCoinbase(1));
                //var utxo = new TxOut
                //{
                //    ScriptPubKey = newAddress.ScriptPubKey,
                //    Value = Money.Coins(5000000)
                //};
                //txNew.AddOutput(utxo);

                //txNew.AddInput(TxIn.CreateCoinbase(0));
                //txNew.AddOutput(new TxOut(Money.Coins(500), newAddress.ScriptPubKey));


                //txNew.AddInput(new TxIn()
                //{
                //    PrevOut = new OutPoint( new uint256("0xcdad8a555b8d574e31d66e11841ce2ffa76074c00e4db8afe1f4bcfc79e520bf"), 0),
                //    ScriptSig = new Script("OP_DUP OP_HASH160 " + newAddress.ScriptPubKey + " OP_EQUALVERIFY OP_CHECKSIG")
                //    //ScriptSig = new Script(Op.GetPushOp(0), new Op()
                //    //{
                //    //    Code = (OpcodeType)0x1,
                //    //    PushData = new[] { (byte)42 }
                //    //}, Op.GetPushOp(NBitcoin.DataEncoders.Encoders.ASCII.DecodeData("The Times 03/Jan/2009 Chancellor on brink of second bailout for banks")))
                //});

                ///////////////
                //byte[] dummyPubKey = TransactionSignature.Empty.ToBytes();

                //byte[] dummyPubKey2 = new byte[33];
                //dummyPubKey2[0] = 0x02;
                ////CBasicKeyStore keystore;
                ////CCoinsView coinsDummy;
                //CoinsView coins = new CoinsView();//(coinsDummy);           
                //Transaction[] dummyTransactions = SetupDummyInputs(coins);//(keystore, coins);

                //Transaction txNew = new Transaction();
                //txNew.Inputs.AddRange(Enumerable.Range(0, 3).Select(_ => new TxIn()));
                //txNew.Inputs[0].PrevOut.Hash = dummyTransactions[0].GetHash();
                //txNew.Inputs[0].PrevOut.N = 1;
                //txNew.Inputs[0].ScriptSig += dummyPubKey;
                //txNew.Inputs[1].PrevOut.Hash = dummyTransactions[1].GetHash();
                //txNew.Inputs[1].PrevOut.N = 0;
                //txNew.Inputs[1].ScriptSig = txNew.Inputs[1].ScriptSig + dummyPubKey + dummyPubKey2;
                //txNew.Inputs[2].PrevOut.Hash = dummyTransactions[1].GetHash();
                //txNew.Inputs[2].PrevOut.N = 1;
                //txNew.Inputs[2].ScriptSig = txNew.Inputs[2].ScriptSig + dummyPubKey + dummyPubKey2;
                //txNew.Outputs.AddRange(Enumerable.Range(0, 2).Select(_ => new TxOut()));
                //txNew.Outputs[0].Value = 90 * Money.CENT;
                //txNew.Outputs[0].ScriptPubKey += OpcodeType.OP_1;
                //////////

                //var newTransactionData = new TransactionData
                //{
                //    Amount = Money.Coins(5000000),
                //    IsCoinStake = true,
                //    BlockHeight = 0,
                //    BlockHash = null,//uint256.Parse("0x833b36d8ff5e461b72d0155cccf51f6b0cd893a904510b26e980f7c58c758481"), //"000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f",
                //    Id = txNew.GetHash(),
                //    CreationTime = DateTimeOffset.FromUnixTimeSeconds(txNew.Time),
                //    Index = txNew.Outputs.IndexOf(txNew.Outputs[0]),
                //    ScriptPubKey = txNew.Outputs[0].ScriptPubKey,
                //    Hex = txNew.ToHex(),
                //    IsPropagated = true,
                //};
                //newAddress.Transactions.Add(newTransactionData);
                //}
                ///////////////////////////////////////////////
                addresses.Add(newAddress);
                addressesCreated.Add(newAddress);
            }

            if (isChange)
            {
                this.InternalAddresses = addresses;
            }
            else
            {
                this.ExternalAddresses = addresses;
            }

            return addressesCreated;
        }

        //Neo//////////////////////
        private Transaction[] SetupDummyInputs(CoinsView coinsRet)
        {
            Transaction[] dummyTransactions = Enumerable.Range(0, 2).Select(_ => new Transaction()).ToArray();

            // Add some keys to the keystore:
            Key[] key = Enumerable.Range(0, 4).Select((_, i) => new Key(i % 2 != 0)).ToArray();


            // Create some dummy input transactions
            dummyTransactions[0].Outputs.AddRange(Enumerable.Range(0, 2).Select(_ => new TxOut()));
            dummyTransactions[0].Outputs[0].Value = 11 * Money.CENT;
            dummyTransactions[0].Outputs[0].ScriptPubKey = dummyTransactions[0].Outputs[0].ScriptPubKey + key[0].PubKey.ToBytes() + OpcodeType.OP_CHECKSIG;
            dummyTransactions[0].Outputs[1].Value = 50 * Money.CENT;
            dummyTransactions[0].Outputs[1].ScriptPubKey = dummyTransactions[0].Outputs[1].ScriptPubKey + key[1].PubKey.ToBytes() + OpcodeType.OP_CHECKSIG;
            coinsRet.AddTransaction(dummyTransactions[0], 0);


            dummyTransactions[1].Outputs.AddRange(Enumerable.Range(0, 2).Select(_ => new TxOut()));
            dummyTransactions[1].Outputs[0].Value = 21 * Money.CENT;
            dummyTransactions[1].Outputs[0].ScriptPubKey = key[2].PubKey.GetAddress(Network.Main).ScriptPubKey;
            dummyTransactions[1].Outputs[1].Value = 22 * Money.CENT;
            dummyTransactions[1].Outputs[1].ScriptPubKey = key[3].PubKey.GetAddress(Network.Main).ScriptPubKey;
            coinsRet.AddTransaction(dummyTransactions[1], 0);


            return dummyTransactions;
        }
        /////////////////////
        /// <summary>
        /// Lists all spendable transactions in the current account.
        /// </summary>
        /// <param name="currentChainHeight">The current height of the chain. Used for calculating the number of confirmations a transaction has.</param>
        /// <param name="confirmations">The minimum number of confirmations required for transactions to be considered.</param>
        /// <returns>A collection of spendable outputs that belong to the given account.</returns>
        public IEnumerable<UnspentOutputReference> GetSpendableTransactions(int currentChainHeight, int confirmations = 0)
        {
            // This will take all the spendable coins that belong to the account and keep the reference to the HDAddress and HDAccount.
            // This is useful so later the private key can be calculated just from a given UTXO.
            foreach (var address in this.GetCombinedAddresses())
            {
                // A block that is at the tip has 1 confirmation.
                // When calculating the confirmations the tip must be advanced by one.

                int countFrom = currentChainHeight + 1;
                foreach (TransactionData transactionData in address.UnspentTransactions())
                {
                    int? confirmationCount = 0;
                    if (transactionData.BlockHeight != null)
                        confirmationCount = countFrom >= transactionData.BlockHeight ? countFrom - transactionData.BlockHeight : 0;

                    if (confirmationCount >= confirmations)
                    {
                        yield return new UnspentOutputReference
                        {
                            Account = this,
                            Address = address,
                            Transaction = transactionData
                        };
                    }
                }
            }
        }
    }

    /// <summary>
    /// An HD address.
    /// </summary>
    public class HdAddress
    {
        public HdAddress()
        {
            this.Transactions = new List<TransactionData>();
        }

        /// <summary>
        /// The index of the address.
        /// </summary>
        [JsonProperty(PropertyName = "index")]
        public int Index { get; set; }

        /// <summary>
        /// The script pub key for this address.
        /// </summary>
        [JsonProperty(PropertyName = "scriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script ScriptPubKey { get; set; }

        /// <summary>
        /// The script pub key for this address.
        /// </summary>
        [JsonProperty(PropertyName = "pubkey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script Pubkey { get; set; }

        /// <summary>
        /// The Base58 representation of this address.
        /// </summary>
        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }

        /// <summary>
        /// A path to the address as defined in BIP44.
        /// </summary>
        [JsonProperty(PropertyName = "hdPath")]
        public string HdPath { get; set; }

        /// <summary>
        /// A list of transactions involving this address.
        /// </summary>
        [JsonProperty(PropertyName = "transactions")]
        public ICollection<TransactionData> Transactions { get; set; }

        /// <summary>
        /// Determines whether this is a change address or a receive address.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if it is a change address; otherwise, <c>false</c>.
        /// </returns>
        public bool IsChangeAddress()
        {
            return HdOperations.IsChangeAddress(this.HdPath);
        }

        /// <summary>
        /// List all spendable transactions in an address.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<TransactionData> UnspentTransactions()
        {
            if (this.Transactions == null)
            {
                return new List<TransactionData>();
            }

            return this.Transactions.Where(t => t.IsSpendable());
        }
    }

    /// <summary>
    /// An object containing transaction data.
    /// </summary>
    public class TransactionData
    {
        /// <summary>
        /// Transaction id.
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 Id { get; set; }

        /// <summary>
        /// The transaction amount.
        /// </summary>
        [JsonProperty(PropertyName = "amount")]
        [JsonConverter(typeof(MoneyJsonConverter))]
        public Money Amount { get; set; }

        /// <summary>
        /// A value indicating whether this is a coin stake transaction or not.
        /// </summary>
        [JsonProperty(PropertyName = "isCoinStake", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsCoinStake { get; set; }

        /// <summary>
        /// The index of this scriptPubKey in the transaction it is contained.
        /// </summary>
        /// <remarks>
        /// This is effectively the index of the output, the position of the output in the parent transaction.
        /// </remarks>
        [JsonProperty(PropertyName = "index", NullValueHandling = NullValueHandling.Ignore)]
        public int Index { get; set; }

        /// <summary>
        /// The height of the block including this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "blockHeight", NullValueHandling = NullValueHandling.Ignore)]
        public int? BlockHeight { get; set; }

        /// <summary>
        /// The hash of the block including this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "blockHash", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 BlockHash { get; set; }

        /// <summary>
        /// Gets or sets the creation time.
        /// </summary>
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }

        /// <summary>
        /// Gets or sets the Merkle proof for this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "merkleProof", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(BitcoinSerializableJsonConverter))]
        public PartialMerkleTree MerkleProof { get; set; }

        /// <summary>
        /// The script pub key for this address.
        /// </summary>
        [JsonProperty(PropertyName = "scriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script ScriptPubKey { get; set; }

        /// <summary>
        /// Hexadecimal representation of this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "hex", NullValueHandling = NullValueHandling.Ignore)]
        public string Hex { get; set; }

        /// <summary>
        /// Propagation state of this transaction.
        /// </summary>
        /// <remarks>Assume it's <c>true</c> if the field is <c>null</c>.</remarks>
        [JsonProperty(PropertyName = "isPropagated", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsPropagated { get; set; }

        /// <summary>
        /// Gets or sets the full transaction object.
        /// </summary>
        [JsonIgnore]
        public Transaction Transaction => this.Hex == null ? null : Transaction.Parse(this.Hex);

        /// <summary>
        /// The details of the transaction in which the output referenced in this transaction is spent.
        /// </summary>
        [JsonProperty(PropertyName = "spendingDetails", NullValueHandling = NullValueHandling.Ignore)]
        public SpendingDetails SpendingDetails { get; set; }

        /// <summary>
        /// Determines whether this transaction is confirmed.
        /// </summary>
        public bool IsConfirmed()
        {
            return this.BlockHeight != null;
        }

        /// <summary>
        /// Indicates an output is spendable.
        /// </summary>
        public bool IsSpendable()
        {
            return this.SpendingDetails == null;
        }

        public Money SpendableAmount(bool confirmedOnly)
        {
            // This method only returns a UTXO that has no spending output.
            // If a spending output exists (even if its not confirmed) this will return as zero balance.
            if (this.IsSpendable())
            {
                // If the 'confirmedOnly' flag is set check that the UTXO is confirmed.
                if (confirmedOnly && !this.IsConfirmed())
                {
                    return Money.Zero;
                }

                return this.Amount;
            }

            return Money.Zero;
        }
    }

    /// <summary>
    /// An object representing a payment.
    /// </summary>
    public class PaymentDetails
    {
        /// <summary>
        /// The script pub key of the destination address.
        /// </summary>
        [JsonProperty(PropertyName = "destinationScriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script DestinationScriptPubKey { get; set; }

        /// <summary>
        /// The Base58 representation of the destination  address.
        /// </summary>
        [JsonProperty(PropertyName = "destinationAddress")]
        public string DestinationAddress { get; set; }

        /// <summary>
        /// The transaction amount.
        /// </summary>
        [JsonProperty(PropertyName = "amount")]
        [JsonConverter(typeof(MoneyJsonConverter))]
        public Money Amount { get; set; }
    }

    public class SpendingDetails
    {
        public SpendingDetails()
        {
            this.Payments = new List<PaymentDetails>();
        }

        /// <summary>
        /// The id of the transaction in which the output referenced in this transaction is spent.
        /// </summary>
        [JsonProperty(PropertyName = "transactionId", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 TransactionId { get; set; }

        /// <summary>
        /// A list of payments made out in this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "payments", NullValueHandling = NullValueHandling.Ignore)]
        public ICollection<PaymentDetails> Payments { get; set; }

        /// <summary>
        /// The height of the block including this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "blockHeight", NullValueHandling = NullValueHandling.Ignore)]
        public int? BlockHeight { get; set; }

        /// <summary>
        /// A value indicating whether this is a coin stake transaction or not.
        /// </summary>
        [JsonProperty(PropertyName = "isCoinStake", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsCoinStake { get; set; }

        /// <summary>
        /// Gets or sets the creation time.
        /// </summary>
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }

        /// <summary>
        /// Hexadecimal representation of this spending transaction.
        /// </summary>
        [JsonProperty(PropertyName = "hex", NullValueHandling = NullValueHandling.Ignore)]
        public string Hex { get; set; }

        /// <summary>
        /// Gets or sets the full transaction object.
        /// </summary>
        [JsonIgnore]
        public Transaction Transaction => this.Hex == null ? null : Transaction.Parse(this.Hex);

        /// <summary>
        /// Determines whether this transaction being spent is confirmed.
        /// </summary>
        public bool IsSpentConfirmed()
        {
            return this.BlockHeight != null;
        }
    }

    /// <summary>
    /// Represents an UTXO that keeps a reference to <see cref="HdAddress"/> and <see cref="HdAccount"/>.
    /// </summary>
    /// <remarks>
    /// This is useful when an UTXO needs access to its HD properties like the HD path when reconstructing a private key.
    /// </remarks>
    public class UnspentOutputReference
    {
        /// <summary>
        /// The account associated with this UTXO
        /// </summary>
        public HdAccount Account { get; set; }

        /// <summary>
        /// The address associated with this UTXO
        /// </summary>
        public HdAddress Address { get; set; }

        /// <summary>
        /// The transaction representing the UTXO.
        /// </summary>
        public TransactionData Transaction { get; set; }

        /// <summary>
        /// Convert the <see cref="TransactionData"/> to an <see cref="OutPoint"/>
        /// </summary>
        /// <returns>The corresponding <see cref="OutPoint"/>.</returns>
        public OutPoint ToOutPoint()
        {
            return new OutPoint(this.Transaction.Id, (uint)this.Transaction.Index);
        }
    }
}
