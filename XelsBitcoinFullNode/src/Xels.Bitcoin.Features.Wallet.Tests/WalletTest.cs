﻿using System.Linq;
using NBitcoin;
using Xunit;

namespace Xels.Bitcoin.Features.Wallet.Tests
{
    public class WalletTest : WalletTestBase
    {
        [Fact]
        public void GetAccountsByCoinTypeReturnsAccountsFromWalletByCoinType()
        {
            var wallet = new Wallet();
            wallet.AccountsRoot.Add(CreateAccountRootWithHdAccountHavingAddresses("XelsAccount", CoinType.Xels));
            wallet.AccountsRoot.Add(CreateAccountRootWithHdAccountHavingAddresses("BitcoinAccount", CoinType.Bitcoin));
            wallet.AccountsRoot.Add(CreateAccountRootWithHdAccountHavingAddresses("XelsAccount2", CoinType.Xels));

            var result = wallet.GetAccountsByCoinType(CoinType.Xels);

            Assert.Equal(2, result.Count());
            Assert.Equal("XelsAccount", result.ElementAt(0).Name);
            Assert.Equal("XelsAccount2", result.ElementAt(1).Name);
        }

        [Fact]
        public void GetAccountsByCoinTypeWithoutAccountsReturnsEmptyList()
        {
            var wallet = new Wallet();

            var result = wallet.GetAccountsByCoinType(CoinType.Xels);

            Assert.Empty(result);
        }

        [Fact]
        public void GetAllTransactionsByCoinTypeReturnsTransactionsFromWalletByCoinType()
        {
            var wallet = new Wallet();
            var xelsAccountRoot = CreateAccountRootWithHdAccountHavingAddresses("XelsAccount", CoinType.Xels);
            var bitcoinAccountRoot = CreateAccountRootWithHdAccountHavingAddresses("BitcoinAccount", CoinType.Bitcoin);
            var xelsAccountRoot2 = CreateAccountRootWithHdAccountHavingAddresses("XelsAccount2", CoinType.Xels);

            var transaction1 = CreateTransaction(new uint256(1), new Money(15000), 1);
            var transaction2 = CreateTransaction(new uint256(2), new Money(91209), 1);
            var transaction3 = CreateTransaction(new uint256(3), new Money(32145), 1);
            var transaction4 = CreateTransaction(new uint256(4), new Money(654789), 1);
            var transaction5 = CreateTransaction(new uint256(5), new Money(52387), 1);
            var transaction6 = CreateTransaction(new uint256(6), new Money(879873), 1);

            xelsAccountRoot.Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Add(transaction1);
            xelsAccountRoot.Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions.Add(transaction2);
            bitcoinAccountRoot.Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Add(transaction3);
            bitcoinAccountRoot.Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions.Add(transaction4);
            xelsAccountRoot2.Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Add(transaction5);
            xelsAccountRoot2.Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions.Add(transaction6);

            wallet.AccountsRoot.Add(xelsAccountRoot);
            wallet.AccountsRoot.Add(bitcoinAccountRoot);
            wallet.AccountsRoot.Add(xelsAccountRoot2);

            var result = wallet.GetAllTransactionsByCoinType(CoinType.Xels).ToList();

            Assert.Equal(4, result.Count);
            Assert.Equal(transaction2, result[0]);
            Assert.Equal(transaction6, result[1]);
            Assert.Equal(transaction1, result[2]);
            Assert.Equal(transaction5, result[3]);
        }

        [Fact]
        public void GetAllTransactionsByCoinTypeWithoutMatchingAccountReturnsEmptyList()
        {
            var wallet = new Wallet();
            var bitcoinAccountRoot = CreateAccountRootWithHdAccountHavingAddresses("BitcoinAccount", CoinType.Bitcoin);

            var transaction1 = CreateTransaction(new uint256(3), new Money(32145), 1);
            var transaction2 = CreateTransaction(new uint256(4), new Money(654789), 1);

            bitcoinAccountRoot.Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Add(transaction1);
            bitcoinAccountRoot.Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions.Add(transaction2);

            wallet.AccountsRoot.Add(bitcoinAccountRoot);

            var result = wallet.GetAllTransactionsByCoinType(CoinType.Xels).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void GetAllTransactionsByCoinTypeWithoutAccountRootReturnsEmptyList()
        {
            var wallet = new Wallet();

            var result = wallet.GetAllTransactionsByCoinType(CoinType.Xels).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void GetAllPubKeysByCoinTypeReturnsPubkeysFromWalletByCoinType()
        {
            var wallet = new Wallet();
            var xelsAccountRoot = CreateAccountRootWithHdAccountHavingAddresses("XelsAccount", CoinType.Xels);
            var bitcoinAccountRoot = CreateAccountRootWithHdAccountHavingAddresses("BitcoinAccount", CoinType.Bitcoin);
            var xelsAccountRoot2 = CreateAccountRootWithHdAccountHavingAddresses("XelsAccount2", CoinType.Xels);
            wallet.AccountsRoot.Add(xelsAccountRoot);
            wallet.AccountsRoot.Add(bitcoinAccountRoot);
            wallet.AccountsRoot.Add(xelsAccountRoot2);

            var result = wallet.GetAllPubKeysByCoinType(CoinType.Xels).ToList();

            Assert.Equal(4, result.Count);
            Assert.Equal(xelsAccountRoot.Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).ScriptPubKey, result[0]);
            Assert.Equal(xelsAccountRoot2.Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).ScriptPubKey, result[1]);
            Assert.Equal(xelsAccountRoot.Accounts.ElementAt(0).InternalAddresses.ElementAt(0).ScriptPubKey, result[2]);
            Assert.Equal(xelsAccountRoot2.Accounts.ElementAt(0).InternalAddresses.ElementAt(0).ScriptPubKey, result[3]);
        }

        [Fact]
        public void GetAllPubKeysByCoinTypeWithoutMatchingCoinTypeReturnsEmptyList()
        {
            var wallet = new Wallet();
            var bitcoinAccountRoot = CreateAccountRootWithHdAccountHavingAddresses("BitcoinAccount", CoinType.Bitcoin);
            wallet.AccountsRoot.Add(bitcoinAccountRoot);

            var result = wallet.GetAllPubKeysByCoinType(CoinType.Xels).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void GetAllPubKeysByCoinTypeWithoutAccountRootsReturnsEmptyList()
        {
            var wallet = new Wallet();

            var result = wallet.GetAllPubKeysByCoinType(CoinType.Xels).ToList();

            Assert.Empty(result);
        }
    }
}
