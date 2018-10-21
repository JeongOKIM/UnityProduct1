package com.remoteaar.remoteaar_2;

import android.annotation.TargetApi;
import android.app.ActionBar;
import android.app.Activity;
import android.app.KeyguardManager;
import android.app.Notification;
import android.app.NotificationManager;
import android.app.admin.DevicePolicyManager;
import android.app.usage.UsageEvents;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothDevice;
import android.content.BroadcastReceiver;
import android.content.ContentValues;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ActivityInfo;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.net.Uri;
import android.os.Bundle;
import android.os.Debug;
import android.os.Handler;
import android.os.Looper;
import android.os.Message;
import android.support.annotation.NonNull;
import android.util.Log;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.widget.Toast;

import com.unity3d.player.UnityPlayer;
import com.unity3d.player.UnityPlayerActivity;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.OutputStream;
import java.util.ArrayList;
import java.util.List;

import BTH.BTHMain;
import BTH.BluetoothShare;
import BTH.DeviceListActivity;
import BTH.MusicStreamTransfer;

import static android.support.v7.appcompat.R.id.info;

/**
 * Created by KSG on 2017-01-10.
 */

public class MainActivity extends UnityPlayerActivity
{
    // region : 主な変数とコールバックの宣言部 ========================================

    public static MainActivity Instance;

    private Handler mMainHandler;

    private static final String TAG = "E_Remote_Unity";

    public static final int MESSAGE_STATE_CHANGE = 1;
    public static final int MESSAGE_DEVICE_NAME = 2;

    public static final String DEVICE_NAME = "device_name";
    public static final String TOAST = "toast";

    private static final int REQUEST_CONNECT_DEVICE_SECURE = 1;
    private static final int REQUEST_ENABLE_BT = 3;

    //Local BTH adapter
    private BluetoothAdapter mBluetoothAdapter = null;
    private BTHMain mBTHService = null;
    private String mConnectedDeviceName = null;

    DeviceListActivity _Device;
    MusicStreamTransfer _MTF;

    boolean _ApplicationReload = false;


    // コールバックの構文 -----------------

    // コールバックインターフェース
    public interface CallbackEvent
    {
        public void doWork(Object... args);
    }

    public class EventRegistration
    {
        private ArrayList<CallbackEvent> _callbackEventList;

        public EventRegistration()
        {
            _callbackEventList = new ArrayList<CallbackEvent>();
        }

        public boolean Add(CallbackEvent _CallbackEvent)
        {
            return _callbackEventList.add(_CallbackEvent);
        }

        public boolean Add_CheckExist(CallbackEvent _CallbackEvent)
        {
            // 既に存在している場合は、追加していない。
            if (_callbackEventList.contains(_CallbackEvent) == true)
                return false;

            return _callbackEventList.add(_CallbackEvent);
        }

        public boolean Contains(CallbackEvent _CallbackEvent)
        {
            return _callbackEventList.contains(_CallbackEvent);
        }

        public boolean Remove(CallbackEvent _CallbackEvent)
        {
            return _callbackEventList.remove(_CallbackEvent);
        }

        public int Size()
        {
            return _callbackEventList.size();
        }

        public void Clear()
        {
            _callbackEventList.clear();
        }

        public void doWork(Object... args)
        {
            for (CallbackEvent _CallbackEvent :
                    _callbackEventList)
            {
                _CallbackEvent.doWork(args);
            }
        }
    }
    // コールバックの構文の終わり ---------------

    // 各主要オーバーライド関数用のコールバック変数
    public EventRegistration _onPause_CallBack;
    public EventRegistration _onResume_CallBack;
    public EventRegistration _onDestroy_CallBack;
    public EventRegistration _onStop_CallBack;
    public EventRegistration _onRestart_CallBack;


    // endregion



    // region : 主なオーバーライド関数 ========================================


    // 主なオーバーライド関数です。いじらずにInit関数とコルベクを使用すること。

    @Override
    public void onCreate(Bundle savedInstanceState)
    {
        super.onCreate(savedInstanceState);

        // 基本変数の初期化
        Instance = this;

        mMainHandler = new Handler();


        _onPause_CallBack = new EventRegistration();
        _onResume_CallBack = new EventRegistration();
        _onDestroy_CallBack = new EventRegistration();
        _onStop_CallBack = new EventRegistration();
        _onRestart_CallBack = new EventRegistration();

        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);


        // その他の初期化
        Init();
    }

    @Override
    public void onStart()
    {
        super.onStart();

        StartFunc();
        Log.d("Unity", "onStart");
    }


    @Override
    public synchronized void onPause()
    {
        super.onPause();

        _onPause_CallBack.doWork();
        Log.d("Unity", "onPause");
    }

    @Override
    public synchronized void onResume()
    {
        super.onResume();

        _onResume_CallBack.doWork();
        Log.d("Unity", "onResume");
    }

    @Override
    public void onRestart()
    {
        super.onRestart();

        _onRestart_CallBack.doWork();
        Log.d("Unity", "onRestart");
    }


    @Override
    protected void onDestroy()
    {
        super.onDestroy();

        _onDestroy_CallBack.doWork();
        Log.d("Unity", "onDestroy");

    }

    @Override
    protected void onStop()
    {
        super.onStop();

        _onStop_CallBack.doWork();
        Log.d("Unity", "onStop");
    }

    @Override
    public boolean onKeyDown(int keyCode, KeyEvent event) {
        if(keyCode == KeyEvent.KEYCODE_POWER)
        {

        }

        return super.onKeyDown(keyCode, event);
    }

       // endregion

    // region : 初期化に関連する関数 ========================================

    boolean mmakeInstantKeyguard = false;

    /**
     * onCreate時に呼び出される初期化関数
     * 初期化するものはここに入れること
     */
    public void Init()
    {
        mBluetoothAdapter = BluetoothAdapter.getDefaultAdapter();
        if(mBluetoothAdapter == null) {
            finish();
            return;
        }

        _Device = new DeviceListActivity();
        _Device.Init(this);

        _MTF = new MusicStreamTransfer();
        _MTF.Init();


        //Lock screen condition check
        KeyguardManager km = (KeyguardManager)getSystemService(Context.KEYGUARD_SERVICE);
        DevicePolicyManager dm = (DevicePolicyManager)getSystemService(Context.DEVICE_POLICY_SERVICE);

        _onPause_CallBack.Add(new CallbackEvent() {
            @Override
            public void doWork(Object... args) {
                if(mBTHService != null) {
                    if(mBTHService.getState() == BTHMain.STATE_CONNECTED)
                    {
                        UnityPlayer.UnitySendMessage("RemoteMain", "PauseFunction", "");
                    }
                }
            }
        });

        /**
         * Resume時に実行する動作を指定
         */
        _onResume_CallBack.Add(new CallbackEvent() {
            @Override
            public void doWork(Object... args) {
                if(mBTHService != null) {
                    Log.d("Unity", "Resume Func");
                    if(mBTHService.getState() == BTHMain.STATE_NONE){
                        Log.d("Unity", "Resume Func : Non Connect");
                        mBTHService.start();
                    }

                    if(mBTHService.getState() == BTHMain.STATE_CONNECTED)
                    {
                        Log.d("Unity", "Connect");
                        UnityPlayer.UnitySendMessage("RemoteMain", "ResumeFunction", "");
                    }
                }
                DevicePolicyManager dm = (DevicePolicyManager)getSystemService(Context.DEVICE_POLICY_SERVICE);
                int keyguardOption = dm.getKeyguardDisabledFeatures(null);
                if(keyguardOption == DevicePolicyManager.KEYGUARD_DISABLE_FEATURES_NONE)
                {
                    Log.d("Unity", "KEYGUARD_DISABLE_FEATURES_NONE");
                }
                else
                {
                    Log.d("Unity", "Keyguard Lock");
                }

            }
        });

        /**
         * Destroy時に実行する動作を指定
         */
        _onDestroy_CallBack.Add(new CallbackEvent() {
            @Override
            public void doWork(Object... args) {
                _ApplicationReload = false;
                if(mmakeInstantKeyguard == true) {
                    //Delete Keyguard feature
                }
//                _ApplicationRestart = false;
            }
        });

        int keyguardOption = dm.getKeyguardDisabledFeatures(null);
        if(keyguardOption == DevicePolicyManager.KEYGUARD_DISABLE_FEATURES_NONE)
        {
            //Add instant keyguard feature
            mmakeInstantKeyguard = true;

        }
    }

    /**
     * onStart時に呼び出される関数
     */
    public void StartFunc()
    {
        if(_ApplicationReload)
            return;

        if (!mBluetoothAdapter.isEnabled()) {
            Intent enableIntent = new Intent(BluetoothAdapter.ACTION_REQUEST_ENABLE);
            startActivityForResult(enableIntent, REQUEST_ENABLE_BT);
        } else {
            if (mBTHService == null) {
                setupChat();
            }
        }

//        _ApplicationReload = true;
    }

    public void SetApplicationStart() { _ApplicationReload = true; }

    public boolean GetApplicationStart() { return _ApplicationReload; }

    private void setupChat() {
        mBTHService = new BTHMain(this, mHandler);
    }
    // endregion

    //region : Other system functions

    /**
     * アプリケーションの権限を得なければならときに呼び出さ
     * @param requestCode リクエストコード
     * @param permissions リクエスト権限
     * @param grantResults 権限許可要求の結果を記録
     */
    @TargetApi(24)
    @Override
    public void onRequestPermissionsResult(int requestCode, @NonNull String[] permissions,
                                           @NonNull int[] grantResults) {

        super.onRequestPermissionsResult(requestCode, permissions, grantResults);

        if(grantResults.length <= 0 || grantResults[0] == PackageManager.PERMISSION_DENIED) {
            switch(requestCode) {
                case DeviceListActivity.PER_ACCESS_COARSE:
                    _Device.stopScanByReceiver();
                    break;
                case DeviceListActivity.PER_ACCESS_FINE:
                    _Device.stopScanByReceiver();
                    break;
                default:
                    break;
            }
        }
        else if(grantResults.length > 0 && grantResults[0] == PackageManager.PERMISSION_GRANTED)
        {
            Log.d("Unity", "Permission granted");
        }
    }

    /**
     * Bluetoothの現在の状態をDiscoverableに変更する
     */
    public void ensureDiscoverable() {
        if(mBluetoothAdapter.getScanMode() !=
                BluetoothAdapter.SCAN_MODE_CONNECTABLE_DISCOVERABLE) {
            Intent discoverableIntent = new Intent(BluetoothAdapter.ACTION_REQUEST_DISCOVERABLE);
            discoverableIntent.putExtra(BluetoothAdapter.EXTRA_DISCOVERABLE_DURATION, 300);
            startActivity(discoverableIntent);
        }
    }

    /**
     * startActivityResultの結果を処理してくれる関数
     * @param requestCode
     * @param resultCode
     * @param data
     */
    public void onActivityResult(int requestCode, int resultCode, Intent data) {
        switch(requestCode) {
            case REQUEST_CONNECT_DEVICE_SECURE:
                if(resultCode == Activity.RESULT_OK) {
                    connectDevice(data, true);
                }
                break;
            case REQUEST_ENABLE_BT:
                if(resultCode == Activity.RESULT_OK) {
                    setupChat();
                } else {
                    finish();
                }
                break;
        }
    }

//endregion

    //region : BTH Functions

    /**
     * Bluetoothのペアリングを試みる
     * @param data アドレスが含まれているIntent
     * @param sercure Secure Mode/Insecure Mode
     */
    @TargetApi(24)
    private void connectDevice(Intent data, boolean sercure) {
        String address = data.getExtras()
                .getString(DeviceListActivity.EXTRA_DEVICE_ADDRESS);
        BluetoothDevice device = mBluetoothAdapter.getRemoteDevice(address);

        Log.d("Unity", device.getAddress() + " + " + device.getName());
        try {
            mBTHService.connect(device);
        }
        catch (Exception err)
        {
            Log.e("Unity", err.getMessage());
        }
    }

    /**
     * アドレスを受けて来て、Bluetoothのペアリングを試みる
     * @param address Bluetoothのアドレス
     */
    public void Connect(final String address){
        Log.d("Unity", "Connect with : " + address);
        Intent data = new Intent();
        data.putExtra(DeviceListActivity.EXTRA_DEVICE_ADDRESS, address);

        try {
            connectDevice(data, true);
        }catch(Exception e)
        {
            String err = (e.getMessage()== null) ?"Conenction failed":e.getMessage();
            Log.e("Unity", err);
        }
    }

    /**
     * デバイスのスキャンを停止する
     */
    public void StopScan(){
        _Device.stopScanByReceiver();
    }

    /**
     * デバイスのスキャンを開始する
     */
    public void DeviceListCall() {
        if(_Device != null)
            _Device.doDiscovery();
    }

    /**
     * Bluetoothを介してメッセージを送信する
     * @param message メッセージの内容
     */
    private void sendMessage(byte[] message) {
        if(mBTHService.getState() != BTHMain.STATE_CONNECTED) {
            return;
        }

        if(message.length > 0) {
            mBTHService.write(message);
        }
    }

    /**
     * Bluetoothのペアリングを解除する
     */
    public synchronized  void Disconnection() {
        mBTHService.Disconnection();
    }

    /**
     * 相手から送信されたメッセージを受信する
     * @return 受信したメッセージ
     */
    public byte[] ReceiveMsg()
    {
        return mBTHService.ReceiveMsg();
    }
    //endregion

    //region : Handler
    /**
     * Handler処理
     */
    private final Handler mHandler = new Handler() {
        @Override
        public void handleMessage(Message msg) {
            switch(msg.what) {
                case MESSAGE_STATE_CHANGE:
                    switch (msg.arg1) {
                        case BTHMain.STATE_CONNECTED:
                            break;
                        case BTHMain.STATE_CONNECTING:
                            break;
                        case BTHMain.STATE_LISTEN:
                        case BTHMain.STATE_NONE:
                            break;
                    }
                    break;
                case MESSAGE_DEVICE_NAME:
                    break;
            }
        }
    };
    //endregion

    //region : Utilties

    /**
     * AndroidのToastメッセージ出力
     * @param _StrMsg 出力するメッセージ
     * @param _Duration 持続時間
     */
    public void ShowToastMsg(final String _StrMsg, final int _Duration) {
        if(Looper.myLooper() == Looper.getMainLooper()) {
            Toast.makeText(getApplicationContext(), _StrMsg, _Duration).show();
        } else {
            Runnable action = new Runnable() {
                @Override
                public void run() {
                    Toast.makeText(getApplicationContext(), _StrMsg, _Duration).show();
                }
            };
            mMainHandler.post(action);
        }
    }

    public void ShowToastMsg(final String _StrMsg) {
        ShowToastMsg(_StrMsg, Toast.LENGTH_SHORT);
    }
    //endregion


    //region : BTH file transfer function

    /**
     * デバイス内の音楽ファイル名のリストを返す
     * @return 音楽ファイル名のリスト
     */
    public String[] SendFileNamelist() { return _MTF.SendFileNamelist(); }

    /**
     * デバイス内の音楽ファイルの絶対パスを返し
     * @return デバイス内の音楽ファイルの絶対パスのリスト
     */
    public String[] SendFileAbsoulePath() { return _MTF.SendFileAbsoulePath(); }

    /**
     * 現在指定されているパスのサブディレクトリのリストを返す
     * @return 現在指定されているパスのサブディレクトリリスト
     */
    public String[] SendSubDirList() {return _MTF.instance.SendSubDirList(); }

    /**
     *  パスをリダイレクト定める
     * @param detail 指定されパス
     */
    public void SetDetailRoot(String detail)
    {
        _MTF.setsPathDetail(detail);
    }

    /**
     * （注意）Androidのバージョンが高まるに応じて使用することができません
     *  デバイス内の特定の音楽ファイルをBluetoothを介して送信する関数
     *  Android Mashmellow以降利用することができないコードに変更（必要な権限がサードパーティ製アプリケーションでは発行できないように変更）
     * @param fileName
     * @param address
     */
    public void SendUriToClient(String fileName, String address){

        //普遍的に使用可能なコード
        //現在ダイアログが浮かぶ問題がある
        //これを省略する方法を模索する必要がある
        File file = new File(fileName);
        if(file == null){
            Log.d("Unity", "Unvalid File");
        }

        Intent sendIntent = new Intent(Intent.ACTION_SEND);
        sendIntent.setPackage("com.android.bluetooth");
        sendIntent.putExtra(BluetoothDevice.EXTRA_DEVICE, address);
        sendIntent.putExtra(Intent.EXTRA_STREAM, Uri.fromFile(file));
        sendIntent.setType("*/*");
        sendIntent.addFlags(Intent.FLAG_ACTIVITY_MULTIPLE_TASK);
        startActivityForResult(sendIntent, BluetoothShare.STATUS_SUCCESS);
    }
    //endregion

    //region : Media File Tag

    /**
     *
     * @param filePath
     * @return
     */
    public ArrayList<String> ID3TagInfoCollector(String filePath){
        return _MTF.ID3TagInfoCollector(filePath);
    }

    /**
     *
     * @return
     */
    public byte[] ReturnFileAlbumImg() {
        return _MTF.ReturnFileAlbumImg();
    }
    //endregion
}
