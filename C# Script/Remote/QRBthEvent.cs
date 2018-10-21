using UnityEngine;
using System.Collections;
using System;
using System.Globalization;

public class QRBthEvent : MonoBehaviour {

    public static QRBthEvent instance;

    [SerializeField]
    GameObject _PopupWindow;
    [SerializeField]
    QRCodeReader_Cam _QRCamSystem;
    [SerializeField]
    GameObject _CamRenderer;

    bool _ReadSuccess;
    string _ReadName;
    string _ReadAddress;

    Coroutine ConnectCoroutine = null;
    Coroutine WaitCoroutine = null;

    public bool IsActive { get { return _PopupWindow.activeSelf; } }

	void Start () {
        instance = this;
        _QRCamSystem.Init();
        _QRCamSystem.OnQRCodeReadStrEvent.AddListener((string bthInfo) => AddressReadAndConnect(bthInfo));
    }

    /// <summary>
    /// QRモードスキャンウィンドウをオンにすると実行されるアクション 
    /// </summary>
    public void PopupWindowOn()
    {
        _PopupWindow.SetActive(true);
        _CamRenderer.SetActive(true);
        _QRCamSystem.QRCodeReader_SetActive(true);
        _ReadSuccess = false;

        if (ConnectCoroutine != null)
            StopCoroutine(ConnectCoroutine);
    }

    /// <summary>
    /// QRモードスキャンでのBluetoothアドレスを取得する関数 
    /// </summary>
    /// <param name="bthInfo">QRコードで読み込んだBluetooth情報</param>
    public void AddressReadAndConnect(string bthInfo)
    {
        if (_ReadSuccess == true)
            return;

        Debug.LogError("Read bth address");

        //ここの検査コードは、フォーマット自体の問題だけ検査するコードである
        //悪意のある不正なアドレス、あるいはQRコードの問題に関することは、今後、再処理するようにする
        try
        {
            
            string[] token = bthInfo.Split('|');
            //最初|に分けたとき、2ではない場合切る
            if (token.Length != 2)
            {
                Debug.LogError("Unable token");
                return;
            }
            //2番目にアドレスが使用可能なアドレスであることを確認する
            //アドレスの形式はxx：xx：xx：xx：xx：xxである
            //16進数で：必ず含まれるので、：を基準に引き裂いたときに6個出なければならない
            string[] addresstoken = token[1].Split(':');

            if (addresstoken.Length != 6)
            {
                //アドレスのフォーマットが合わないため、案内メッセージを撃つ与える返し
                Debug.LogError("Num of token diffrence");
                return;
            }

            //すべてのアドレスの個々の値は、16進数に従っので、16進数に対応していないアドレスがある場合、そのアドレスの値は、虚偽である
            bool isPossibleAddress = true;
            int temp;
            CultureInfo provider = CultureInfo.CurrentCulture;      //特定のカルチャのinfo（名前、書き込みシステム、使用カレンダー、文字列ジョンリョルスンなどの）を提供してい
                                                                    //下のTryParse関数を使用するために定義してくれる

            for (int i = 0; i < addresstoken.Length; ++i)
            {
                //NumberStyles.AllowHexSpecifierを使用してくれると16進数の変換が可能である
                if (int.TryParse(addresstoken[i], NumberStyles.AllowHexSpecifier, provider,out temp) == false)
                {
                    isPossibleAddress = false;
                    break;
                }
            }

            if (isPossibleAddress == false)
            {
                //有効なアドレスがないため、案内メッセージを撃つ与える返し
                Debug.LogError("No hex number come");
                return;
            }

            //利用可能なアドレスです。試みをして
            //だめならここの問題ではなく、QRコードの問題なので、QRコードを再製作するように誘導をしてくれ
            _ReadName = token[0];
            _ReadAddress = token[1];

            //処理プロセスを、後に1つ作成
            _ReadSuccess = true;

            Invoke("ConnectQRBTH", 0.5f);   //処理順序上の問題でエラーが発生するため、Invokeで順序を調整してくれる


        }
        catch(Exception e)
        {
            Debug.LogError(e);
        }
    }

    /// <summary>
    ///QRコードポップアップをOffするときに呼び出される関数 
    /// </summary>
    public void PopupWindowOff()
    {
        if (_PopupWindow.activeSelf)
        {
            _PopupWindow.SetActive(false);
            _QRCamSystem.QRCodeReader_SetActive(false);
            if (ConnectCoroutine != null)
                StopCoroutine(ConnectCoroutine);
            if (WaitCoroutine != null)
                StopCoroutine(WaitCoroutine);

            ConnectCoroutine = null;
            WaitCoroutine = null;
            _ReadSuccess = false;

            AARController.Instance.Call("DeviceListCall");
        }
    }

    /// <summary>
    ///QRコードで読み取ったブルートゥースアドレスを使用してペアリングをしようとするときに呼び出される関数 
    /// </summary>
    void ConnectQRBTH()
    {
        AARController.Instance.Call("StopScan");
        try
        {
            RemoteMain.instance.Connect(_ReadName, _ReadAddress);
        }
        catch (Exception e)
        {
            Debug.LogError("Device address : " + _ReadAddress + "Connection Failed  : " + e.StackTrace);
        }

        _QRCamSystem.QRCodeReader_SetActive(false);
    }

    /// <summary>
    /// Bluetooth接続の試行時、一定時間が経過すると無効な接続と判断し、再接続を試行するように接続を切断する関数 
    /// </summary>
    IEnumerator WaitConnect()
    {
        float time = 10f;

        while (time > 0)
        {
            yield return null;
            time -= Time.deltaTime;
        }

        StopCoroutine(ConnectCoroutine);

        yield return new WaitForEndOfFrame();       //ここで、接続失敗ウィンドウを浮かべてくれる

        _QRCamSystem.QRCodeReader_SetActive(true);
        _ReadSuccess = false;
    }

    /// <summary>
    /// Bluetoothのアドレスを正確に読んで来た時に呼び出される関数 
    /// </summary>
    public void SuccessRead()
    {
        Debug.LogError("Success Read");
        _QRCamSystem.QRCodeReader_SetActive(false);
        //ロード部分を実行する

        StartCoroutine(WaitConnect());
    }

    /// <summary>
    /// QRコードスキャンを再度実行してくれる関数
    /// </summary>
    public void RestartQRReader()
    {
        _ReadSuccess = false;
        _QRCamSystem.QRCodeReader_SetActive(true);
        if (WaitCoroutine != null)
        {
            StopCoroutine(WaitCoroutine);
            WaitCoroutine = null;
        }
    }
}
