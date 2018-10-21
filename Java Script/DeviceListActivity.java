package BTH;

import android.Manifest;
import android.annotation.TargetApi;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.le.BluetoothLeScanner;
import android.bluetooth.le.ScanCallback;
import android.bluetooth.le.ScanResult;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.pm.PackageManager;
import android.os.Build;
import android.support.annotation.Dimension;
import android.support.v4.app.ActivityCompat;
import android.support.v4.content.ContextCompat;
import android.util.Log;

import com.remoteaar.remoteaar_2.MainActivity;
import com.unity3d.player.UnityPlayer;

import java.lang.reflect.Method;
import java.util.List;
import java.util.Set;

/**
 * Created by KJO on 2017-01-10.
 */

public class DeviceListActivity {

    private static final String TAG = "DeviceListActivity";

    public static String EXTRA_DEVICE_ADDRESS = "device_address";

    private BluetoothAdapter mBtAdapter;
    private BluetoothLeScanner mBLEScanner;
    private ScanCallback mScanCallback;

    public static final int PER_ACCESS_COARSE = 1;
    public static final int PER_ACCESS_FINE = 2;

    MainActivity _Activity;

    @TargetApi(24)
    public void Init(MainActivity activity) {
        _Activity = activity;

        //Bluetoothデバイスを見つけたときのCallBack
        IntentFilter filter = new IntentFilter(BluetoothDevice.ACTION_FOUND);
        _Activity.registerReceiver(mReceiver, filter);

        //Bluetoothデバイススキャンが終わったときのCallBack
        filter = new IntentFilter(BluetoothAdapter.ACTION_DISCOVERY_FINISHED);
        _Activity.registerReceiver(mReceiver, filter);

        //Bluetoothデバイスのペアリング要求が来たときのCallBack
        filter = new IntentFilter(BluetoothDevice.ACTION_PAIRING_REQUEST);
        _Activity.registerReceiver(mReceiver, filter);

        mBtAdapter = BluetoothAdapter.getDefaultAdapter();

        //Android api level24以上のデバイス使用時に必要なBluetooth Low Energyの構文
        if(Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            mBLEScanner = mBtAdapter.getBluetoothLeScanner();

            mScanCallback = new ScanCallback() {
                @Override
                public void onScanResult(int callbackType, ScanResult result) {
                    super.onScanResult(callbackType, result);
                    processResult(result);
                }

                @Override
                public void onBatchScanResults(List<ScanResult> results) {
                    for(ScanResult result : results) {
                        processResult(result);
                    }
                }

                @Override
                public void onScanFailed(int errorCode) {
                    Log.d(TAG, "Scan failed with " + errorCode);
                }

                private void processResult(final ScanResult result) {
                    Runnable action = new Runnable() {
                        @Override
                        public void run() {
                            BluetoothDevice device = result.getDevice();
                            if(device.getBondState() != BluetoothDevice.BOND_BONDED){
                                //Unityに転送してくれる関数が必要
                                String str = device.getName() + "|" + device.getAddress();
                                UnityPlayer.UnitySendMessage("DeviceList", "FindDevice", str);
                            }
                        }
                    };

                    _Activity.runOnUiThread(action);
                }
            };
        } else {
            mBLEScanner = null;
            mScanCallback = null;
        }

        Set<BluetoothDevice> pairedDevices = mBtAdapter.getBondedDevices();

        if(pairedDevices.size() > 0) {
            for(BluetoothDevice device : pairedDevices) {
                //Unityに転送してくれる関数が必要
                String str = device.getName() + "|" + device.getAddress();
                UnityPlayer.UnitySendMessage("DeviceList", "PairedDevice", str);
            }
        } else {

        }
    }

    @TargetApi(24)
    public void Destroy() {
        if(mBtAdapter != null) {
            mBtAdapter.cancelDiscovery();
        }

        if(Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            if(mBLEScanner != null) {
                mBLEScanner.stopScan(mScanCallback);
            }
        }

        _Activity.unregisterReceiver(mReceiver);
    }

    /**
     * Bluetoothのデバイススキャンを実行し
     */
    @TargetApi(24)
    public void doDiscovery() {
        if(mBtAdapter.isDiscovering()){
            mBtAdapter.cancelDiscovery();
        }

        mBtAdapter.startDiscovery();

        //Android api lv24以上の場合、必要なアクセス許可要求の構文
        if(Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            checkPermissionAllow(Manifest.permission.ACCESS_COARSE_LOCATION);
            checkPermissionAllow(Manifest.permission.ACCESS_FINE_LOCATION);

            mBLEScanner.startScan(mScanCallback);
        }
    }

    /**
     * アクセス許可要求の構文
     * @param Permission 要求権限
     */
    @TargetApi(24)
    private void checkPermissionAllow(String Permission) {
        int permissionCheck =
                ContextCompat.checkSelfPermission(_Activity, Permission);

        if(permissionCheck == PackageManager.PERMISSION_DENIED) {
            int requestCode = 0;
            switch (Permission) {
                case android.Manifest.permission.ACCESS_COARSE_LOCATION:
                    requestCode = PER_ACCESS_COARSE;
                    break;
                case Manifest.permission.ACCESS_FINE_LOCATION:
                    requestCode = PER_ACCESS_FINE;
                    break;
            }
            ActivityCompat.requestPermissions(_Activity, new String[] { Permission }, requestCode);
        }

    }

    /**
     * Bluetoothのデバイススキャンを止め
     */
    @TargetApi(24)
    public void stopScanByReceiver() {
        if(mBLEScanner != null) {
            mBLEScanner.stopScan(mScanCallback);
        }
    }

    /**
     * Bluetoothのペアリングを解除
     * @param device 解除する装置
     */
    public void unpairDevice(BluetoothDevice device){
        try{
            Method m = device.getClass().getMethod("removeBond", (Class[])null);
            m.invoke(device, (Object[]) null);
        } catch(Exception e) {
            Log.d("Unity", e.getMessage());
        }
    }


    /**
     * Bluetoothで特定のActionを受信する場合、いくつかの関数を呼び出すかを決定する汎用Receiver
     */
    private final BroadcastReceiver mReceiver = new BroadcastReceiver() {
        @Override
        public void onReceive(Context context, Intent intent) {
            String action = intent.getAction();
            // When discovery finds a device
            if (BluetoothDevice.ACTION_FOUND.equals(action)) {
                // Get the BluetoothDevice object from the Intent
                BluetoothDevice device = intent.getParcelableExtra(BluetoothDevice.EXTRA_DEVICE);
                String str = device.getName() + "|" + device.getAddress();
                UnityPlayer.UnitySendMessage("DeviceList", "FindDevice", str);
            } else if (BluetoothAdapter.ACTION_DISCOVERY_FINISHED.equals(action)) {
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                    //LE Scan stop
                    stopScanByReceiver();
                }
            }
        }
    };
}
