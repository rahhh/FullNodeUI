import { Injectable } from '@angular/core';
import { Http, Headers, Response, RequestOptions, URLSearchParams} from '@angular/http';
import { Observable } from 'rxjs/Observable';
import 'rxjs/add/operator/map';
import 'rxjs/add/operator/switchMap';
import 'rxjs/add/operator/catch';
import "rxjs/add/observable/interval";
import 'rxjs/add/operator/startWith';

import { GlobalService } from './global.service';
import { ElectronService } from 'ngx-electron';

import { WalletCreation } from '../classes/wallet-creation';
import { WalletRecovery } from '../classes/wallet-recovery';
import { WalletLoad } from '../classes/wallet-load';
import { WalletInfo } from '../classes/wallet-info';
import { Mnemonic } from '../classes/mnemonic';
import { FeeEstimation } from '../classes/fee-estimation';
import { TransactionBuilding } from '../classes/transaction-building';
import { TransactionSending } from '../classes/transaction-sending';

/**
 * For information on the API specification have a look at our swagger files located at http://localhost:5000/swagger/ when running the daemon
 */
@Injectable()
export class ApiService {
    constructor(private http: Http, private globalService: GlobalService, private electronService: ElectronService) {
      this.setApiPort();
    };

    private headers = new Headers({'Content-Type': 'application/json'});
    private pollingInterval = 3000;
    private apiPort;
    private xelsApiUrl;

    setApiPort() {
      this.apiPort = this.electronService.ipcRenderer.sendSync('get-port');
      this.xelsApiUrl = 'http://localhost:' + this.apiPort + '/api';
    }

    /**
     * Gets available wallets at the default path
     */
    getWalletFiles(): Observable<any> {
      return this.http
        .get(this.xelsApiUrl + '/wallet/files')
        .map((response: Response) => response);
     }

     /**
      * Get a new mnemonic
      */
    getNewMnemonic(): Observable<any> {
      let params: URLSearchParams = new URLSearchParams();
      params.set('language', 'English');

      params.set('wordCount', '12');

      return this.http
        .get(this.xelsApiUrl + '/wallet/mnemonic', new RequestOptions({headers: this.headers, search: params}))
        .map((response: Response) => response);
    }

    /**
     * Create a new Xels wallet.
     */
    createXelsWallet(data: WalletCreation): Observable<any> {
      return this.http
        .post(this.xelsApiUrl + '/wallet/create/', JSON.stringify(data), {headers: this.headers})
        .map((response: Response) => response);
    }

    /**
     * Recover a Xels wallet.
     */
    recoverXelsWallet(data: WalletRecovery): Observable<any> {
      return this.http
        .post(this.xelsApiUrl + '/wallet/recover/', JSON.stringify(data), {headers: this.headers})
        .map((response: Response) => response);
    }

    /**
     * Load a Xels wallet
     */
    loadXelsWallet(data: WalletLoad): Observable<any> {
      return this.http
        .post(this.xelsApiUrl + '/wallet/load/', JSON.stringify(data), {headers: this.headers})
        .map((response: Response) => response);
    }

    /**
     * Get wallet status info from the API.
     */
    getWalletStatus(): Observable<any> {
      return this.http
        .get(this.xelsApiUrl + '/wallet/status')
        .map((response: Response) => response);
    }

    /**
     * Get general wallet info from the API once.
     */
    getGeneralInfoOnce(data: WalletInfo): Observable<any> {
      let params: URLSearchParams = new URLSearchParams();
      params.set('Name', data.walletName);

      return this.http
        .get(this.xelsApiUrl + '/wallet/general-info', new RequestOptions({headers: this.headers, search: params}))
        .map((response: Response) => response);
    }

    /**
     * Get general wallet info from the API.
     */
    getGeneralInfo(data: WalletInfo): Observable<any> {
      let params: URLSearchParams = new URLSearchParams();
      params.set('Name', data.walletName);

      return Observable
        .interval(this.pollingInterval)
        .startWith(0)
        .switchMap(() => this.http.get(this.xelsApiUrl + '/wallet/general-info', new RequestOptions({headers: this.headers, search: params})))
        .map((response: Response) => response);
    }

    /**
     * Get wallet balance info from the API.
     */
    getWalletBalance(data: WalletInfo): Observable<any> {
      let params: URLSearchParams = new URLSearchParams();
      params.set('walletName', data.walletName);
      params.set('accountName', "account 0");

      return Observable
        .interval(this.pollingInterval)
        .startWith(0)
        .switchMap(() => this.http.get(this.xelsApiUrl + '/wallet/balance', new RequestOptions({headers: this.headers, search: params})))
        .map((response: Response) => response);
    }

    /**
     * Get the maximum sendable amount for a given fee from the API
     */
    getMaximumBalance(data): Observable<any> {
      let params: URLSearchParams = new URLSearchParams();
      params.set('walletName', data.walletName);
      params.set('accountName', "account 0");
      params.set('feeType', data.feeType);
      params.set('allowUnconfirmed', "true");

      return this.http
        .get(this.xelsApiUrl + '/wallet/maxbalance', new RequestOptions({headers: this.headers, search: params}))
        .map((response: Response) => response);
    }

    /**
     * Get a wallets transaction history info from the API.
     */
    getWalletHistory(data: WalletInfo): Observable<any> {
      let params: URLSearchParams = new URLSearchParams();
      params.set('walletName', data.walletName);
      params.set('accountName', "account 0");

      return Observable
        .interval(this.pollingInterval)
        .startWith(0)
        .switchMap(() => this.http.get(this.xelsApiUrl + '/wallet/history', new RequestOptions({headers: this.headers, search: params})))
        .map((response: Response) => response);
    }

    /**
     * Get an unused receive address for a certain wallet from the API.
     */
    getUnusedReceiveAddress(data: WalletInfo): Observable<any> {
      let params: URLSearchParams = new URLSearchParams();
      params.set('walletName', data.walletName);
      params.set('accountName', "account 0"); //temporary

      return this.http
        .get(this.xelsApiUrl + '/wallet/unusedaddress', new RequestOptions({headers: this.headers, search: params}))
        .map((response: Response) => response);
    }

    /**
     * Get multiple unused receive addresses for a certain wallet from the API.
     */
    getUnusedReceiveAddresses(data: WalletInfo, count: string): Observable<any> {
      let params: URLSearchParams = new URLSearchParams();
      params.set('walletName', data.walletName);
      params.set('accountName', "account 0"); //temporary
      params.set('count', count);

      return this.http
        .get(this.xelsApiUrl + '/wallet/unusedaddresses', new RequestOptions({headers: this.headers, search: params}))
        .map((response: Response) => response);
    }

    /**
     * Get get all addresses for an account of a wallet from the API.
     */
    getAllAddresses(data: WalletInfo): Observable<any> {
      let params: URLSearchParams = new URLSearchParams();
      params.set('walletName', data.walletName);
      params.set('accountName', "account 0"); //temporary

      return this.http
        .get(this.xelsApiUrl + '/wallet/addresses', new RequestOptions({headers: this.headers, search: params}))
        .map((response: Response) => response);
    }

    /**
     * Estimate the fee of a transaction
     */
    estimateFee(data: FeeEstimation): Observable<any> {
      let params: URLSearchParams = new URLSearchParams();
      params.set('walletName', data.walletName);
      params.set('accountName', data.accountName);
      params.set('destinationAddress', data.destinationAddress);
      params.set('amount', data.amount);
      params.set('feeType', data.feeType);
      params.set('allowUnconfirmed', "true");

      return this.http
        .get(this.xelsApiUrl + '/wallet/estimate-txfee', new RequestOptions({headers: this.headers, search: params}))
        .map((response: Response) => response);
    }

    /**
     * Build a transaction
     */
    buildTransaction(data: TransactionBuilding): Observable<any> {
      return this.http
        .post(this.xelsApiUrl + '/wallet/build-transaction', JSON.stringify(data), {headers: this.headers})
        .map((response: Response) => response);
    }

    /**
     * Send transaction
     */
    sendTransaction(data: TransactionSending): Observable<any> {
      return this.http
        .post(this.xelsApiUrl + '/wallet/send-transaction', JSON.stringify(data), {headers: this.headers})
        .map((response: Response) => response);
    }

    /**
     * Start staking
     */
    startStaking(data: any): Observable<any> {
      return this.http
        .post(this.xelsApiUrl + '/miner/startstaking', JSON.stringify(data), {headers: this.headers})
        .map((response: Response) => response);
    }

    /**
     * Get staking info
     */
    getStakingInfo(): Observable<any> {
      return Observable
        .interval(this.pollingInterval)
        .startWith(0)
        .switchMap(() => this.http.get(this.xelsApiUrl + '/miner/getstakinginfo'))
        .map((response: Response) => response);
    }

    /**
      * Stop staking
      */
    stopStaking(): Observable<any> {
      return this.http
        .post(this.xelsApiUrl + '/miner/stopstaking', {headers: this.headers})
        .map((response: Response) => response);
    }

    /**
     * Send shutdown signal to the daemon
     */
    shutdownNode(): Observable<any> {
      return this.http
        .post(this.xelsApiUrl + '/node/shutdown', {headers: this.headers})
        .map((response: Response) => response);
    }
}
