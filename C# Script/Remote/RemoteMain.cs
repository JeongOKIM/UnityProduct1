using UnityEngine;
using System.Collections;
using System.Text;

public class RemoteMain : MonoBehaviour {

#region Parameter
    public static RemoteMain instance;

    #region Class parameter
    private AARController _AAR;
    private MsgController _Msg;
    private BthCommunicator _Bth;
    private UIActor _UI;
    private DeviceListScene _List;
    private MainBGScene _Main;
    private Fav _Fav;
    private AlertManager _Alert;
    #endregion

    #region Local Parameter
    string _FileName = "LocalRemainDat.dat";
    private string _LastAccessDeviceName = "null";
    
    //아래의 두 parameter는 같은 주소를 가리키는 경우가 많으나 용도가 다르다
    private string _LastAccessAddress = "null";     //Data에 저장되는 마지막으로 연결된 주소. 만약 연결이 실패할 경우 사라진다
    private string _LastDeviceAddress = "null";     //가장 마지막으로 연결되었던 장치의 주소. 연결에 실패하더라도 사라지지 않는다
    private static float COROUTINE_BREAKOUT_TIME = 40.0f;
    #endregion

    #region System Parameter
    private bool _Connect = false;

    private Coroutine _ValueTimeOutCoroutine = null;
    #endregion
#endregion


     void Start () {
        _AAR = AARController.Instance;
        _Msg = MsgController.instance;
        _Bth = BthCommunicator.instance;
        _UI = UIActor.instance;
        _List = DeviceListScene.instance;
        _Main = MainBGScene.instance;
        _Fav = Fav.instance;
        _Alert = AlertManager.instance;

        Screen.SetResolution(800, 1280, true);
        Screen.orientation = ScreenOrientation.Portrait;

        MainInit();
	}

    void Awake()
    {
        instance = this;
    }

    void Update()
    {
#if UNITY_ANDROID
        //Backキーを押して、Bluetoothのペアリングを解除したり、アプリケーションをオフにできるようにBackキー入力を待つようにする
        //Androidでは、BackキーがEscapeコードで入ってくるので
        if (Input.GetKey(KeyCode.Escape))
        {
            if(_Connect == true)
            {
                Disconnection();
            }
            else
            {
                Application.Quit();
            }
        }
#endif
    }

    /// <summary>
    /// スクリプト上の基本的なinit
    /// </summary>
    void MainInit()
    {
        //Initする必要がある場合、この関数では、すべてを解決
        //Awake段やこのようなのにで触れずに、ここですべてを解決できるように

        _UI.UIInit(this, _List, _Main, _Fav);
        _List.ListInit(this, _UI, _Bth, _AAR);
        _Bth.CommunicatorInit(this, _AAR, _UI, _Msg, _Fav);
        _Msg.MsgInit(this, _Main, _Bth, _AAR, _Fav, _Alert);
        _Main.MainBGSceneInit(_AAR, _Msg, _List, _Bth);
        _Fav.FavInit(this, _Main, _Bth, _Alert);
        _Alert.AlertInit();

        RemoteInit();
    }

    /// <summary>
    /// AAR呼び出しを含むBluetooth準備動作
    /// </summary>
    void RemoteInit()
    {
        DataLoad();

        //Dataを別々に保存することなく、このようにしてくれる
        _LastDeviceAddress = _LastAccessAddress;

        _List.gameObject.SetActive(true);
        _Main.gameObject.SetActive(false);
        _AAR.Call("DeviceListCall");

        _AAR.Call("SetApplicationStart");
    }


    #region Remote Connection related Function
    /// <summary>
    /// 本体とのペアリングを試み
    /// </summary>
    /// <param name="name">本体機器名</param>
    /// <param name="address">本体Bluetoothアドレス</param>
    public void Connect(string name, string address)
    {
        _LastAccessAddress = address;
        _LastAccessDeviceName = name;

        //ロードプロセスの実行
        _Main.gameObject.SetActive(true);
        _Main.FirstValueReady();

        //MsgHandler オープン
        MsgHandler.instance.Connect();

        //コネクトコマンド送信
        _AAR.Call("Connect", address);

        //コネクトされたが、最初Valueが到着していなくて発生する可能性のある問題を解決するためのCoroutineを作成
        BreakTimeOutCoroutine();
        _ValueTimeOutCoroutine = StartCoroutine(TimeOutCoroutine());
        

    }

    /// <summary>
    /// 本体からデバイスへのコネクト要求が入ってくる
    /// </summary>
    /// <param name="infoString">本体のBluetooth情報</param>
    public void ClientConnect(string infoString)
    {
        Debug.LogError("Client Connect");

        string[] token = infoString.Split('|');

        _LastAccessAddress = token[1];
        _LastAccessDeviceName = token[0];

        Debug.LogError("Address" + token[1]);

        //ロードプロセスの実行
        _Main.gameObject.SetActive(true);
        _Main.FirstValueReady();

        //MsgHandler オープン
        MsgHandler.instance.Connect();

        //コネクトされたが、最初Valueが到着していなくて発生する可能性のある問題を解決するためのCoroutineを作成
        BreakTimeOutCoroutine();
        _ValueTimeOutCoroutine = StartCoroutine(TimeOutCoroutine());
    }

    /// <summary>
    /// 最後にペアリングした機器とのペアリングを試みる
    /// </summary>
    public void ConnectLastDevice()
    {
        //nullの場合、動作しないようにしてくれる
        //ただし、今後のUI確定時nullアンウーボタンを無効にしてくれる案も検討する
        if (!_LastDeviceAddress.Equals("null"))
        {
            ////ロードプロセスの実行
            _Main.gameObject.SetActive(true);
            _Main.FirstValueReady();

            _AAR.Call("Connect", _LastDeviceAddress);

            //MsgHandler オープン
            MsgHandler.instance.Connect();

            //コネクトされたが、最初Valueが到着していなくて発生する可能性のある問題を解決するためのCoroutineを作成
            BreakTimeOutCoroutine();
            _ValueTimeOutCoroutine = StartCoroutine(TimeOutCoroutine());

        }
    }

    /// <summary>
    /// ペアリングが成立/失敗した後に実行してくれるとする構文
    /// </summary>
    /// <param name="isSuccess">ペアリングが成立されたか</param>
    public void Connected(string isSuccess)
    {
        //Timeout Coroutine オフ
        BreakTimeOutCoroutine();

        //AARのConnected完了
        //あるいはConnectedすることができないので、接続できない
        //それぞれの状況に応じた手順を想定してくれる
        if (isSuccess.Equals("true") == true)
        {
            //接続が成立した
            DataSave();
            _Connect = true;
            _Bth.State = BthCommunicator.COMMUICATOR_STATE.CS_INITIALDAT;

            //最後に接続されたアドレスが変わったので、変更してくれる
            _LastDeviceAddress = _LastAccessAddress;
            QRBthEvent.instance.PopupWindowOff();

            StartCoroutine(FirstDataTimeoutCoroutine());
        }
        else
        {
            //接続が成立していなかった
            Debug.LogError("Connection try failed");
            _LastAccessAddress = "null";
            _LastAccessDeviceName = "null";
            DataSave();
            _Bth.State = BthCommunicator.COMMUICATOR_STATE.CS_DISCONNECT;
            _Main.ReleaseFirstValue();
            _List.Disconnect();

            QRBthEvent.instance.RestartQRReader();
        }
    }

    /// <summary>
    /// 本体の最初の状態データの受信制限時間
    /// </summary>
    /// <returns></returns>
    IEnumerator FirstDataTimeoutCoroutine()
    {
        float limitTime = 3f;

        while(_Bth.State != BthCommunicator.COMMUICATOR_STATE.CS_NORMAL)
        {
            limitTime -= Time.deltaTime;

            if(limitTime > 0)
                yield return null;
            else
            {
                if (_Connect == true)
                {
                    Disconnect();
                    Debug.LogError("Init msg Timeout from remote. Disconnect");
                }
                break;
            }
        }
    }

    /// <summary>
    /// 本体とのペアリングを切断
    /// </summary>
    public void Disconnect()
    {
        _ValueTimeOutCoroutine = null;
        _Connect = false;
        _Bth.State = BthCommunicator.COMMUICATOR_STATE.CS_DISCONNECT;
        //Device Listをオンに与えScanを開始する
        _List.gameObject.SetActive(true);
        _Main.gameObject.SetActive(false);

        _List.Disconnect();

        //Disconnect時に元の状態に還元させてこそするもの一覧
        MsgHandler.instance.Disconnection();
        Fav.instance.FavDisconnect();
    }

    /// <summary>
    /// 本体とのペアリングを切断（AARコール）
    /// </summary>
    public void Disconnection()
    {
        _AAR.Call("Disconnection");
    }

    /// <summary>
    /// Pause状態では、Regular check functionをオフにするため
    /// </summary>
    public void PauseFunction()
    {
        _Msg.StopCheckStatus();
    }

    /// <summary>
    /// Resume状態になったときPauseとき止めておいたいくつかの機能を生かすために呼び出さ
    /// </summary>
    public void ResumeFunction()
    {
        RequestChange();

        Debug.LogError("Resume Function");

        //Normalのときだけこれオンば良い
        if (_Bth.State == BthCommunicator.COMMUICATOR_STATE.CS_NORMAL)
            _Msg.StartCheckStatus();
    }

    /// <summary>
    /// 定期的に本体との状態同期のために呼び出される関数
    /// </summary>
    public void RequestChange()
    {
         MsgHandler.instance.GenerateMsg(MsgController.DATA_TYPE.DT_REQUEST_PAUSE, MsgController.DATA_ACTION.DA_NO_REQUIRE_ACTION, null);
    }

    /// <summary>
    /// 送受信制限時間をかけて時間内に送受信していない場合はDisconnectをするようにしてくれる
    /// </summary>
    /// <returns></returns>
    private IEnumerator TimeOutCoroutine()
    {
        float fTime = COROUTINE_BREAKOUT_TIME;

        while (true)
        {
            fTime -= Time.deltaTime;

            if (fTime >= 0)
                yield return null;
            else
                break;
        }

        //問題が生じたのでDisconnectをかけてくれて再接続するように誘導する
        Disconnection();
    }

    /// <summary>
    /// 時間制限コルーチンを強制的に終了
    /// </summary>
    private void BreakTimeOutCoroutine()
    {
        if (_ValueTimeOutCoroutine != null)
        {
            StopCoroutine(_ValueTimeOutCoroutine);
            _ValueTimeOutCoroutine = null;
        }
    }
    #endregion


    #region Local Remain data I/O
    /// <summary>
    /// 最後に接続した機器の情報を保存
    /// </summary>
    private void DataSave()
    {
        //すべてのデータを一度に保存する
        JFile _File;
        _File = JFile.CreateBinary(_FileName, false);
        if (_File == null)
        {
            
        }
        else
        {
            var writer = _File._binaryWriter;
            writer.Write(_LastAccessAddress);
            writer.Write(_LastAccessDeviceName);

            _File.Close();
        }
    }

    /// <summary>
    /// 最後に接続した機器の情報をロード
    /// </summary>
    private void DataLoad()
    {
        JFile _File;
        _File = JFile.CreateBinary(_FileName, true);
        if (_File != null)
        {
            var reader = _File._binaryReader;
            _LastAccessAddress = reader.ReadString();
            _LastAccessDeviceName = reader.ReadString();
            _File.Close();
        }
        else
        {
            _LastAccessAddress = "null";
            _LastAccessDeviceName = "null";
        }
    }
    #endregion
}


