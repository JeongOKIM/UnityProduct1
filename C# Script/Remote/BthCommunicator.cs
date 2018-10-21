using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/// <summary>
///Bluetoothとの通信を担当するクラス 
/// </summary>

public class BthCommunicator : MonoBehaviour {

    public static BthCommunicator instance;

    AARController _AAR;
    UIActor _Actor;
    MsgController _Msg;
    RemoteMain _Main;
    Fav _Fav;

    /// <summary>
    /// 順次処理するメッセージを含んでいるコンテナ
    /// </summary>
    List<byte[]> _MsgSaver;     

    float SEND_MSG_DELAY = 0f;
    float _CurrentDelay = 0f;
    bool _CanSendMsg = true;

    /// <summary>
    /// 現在、Bluetooth通信の状態を示すenum構文
    /// </summary>
    public enum COMMUICATOR_STATE
    {
        CS_DISCONNECT,
        CS_NORMAL,
        CS_INITIALDAT,
        CS_FAVDAT,
        CS_REQUEST_LEAK_STREAM,
    }

    private COMMUICATOR_STATE _State;
    public COMMUICATOR_STATE State { get { return _State; } set { _State = value; } }

    void Awake()
    {
        instance = this;
    }

    void Update()
    {
        if (_MsgSaver.Count > 0)
            MsgController();
    }

    public void CommunicatorInit(RemoteMain main, AARController aar, UIActor actor,
        MsgController control, Fav fav)
    {
        _AAR = aar;
        _Msg = control;
        _Main = main;
        _Actor = actor;
        _Fav = fav;

        _MsgSaver = new List<byte[]>();
        _State = COMMUICATOR_STATE.CS_DISCONNECT;
    }


    /// <summary>
    /// AARにメッセージを送る関数
    /// </summary>
    /// <param name="msg">送信メッセージの内容</param>
    /// <param name="SkipSendMsg">メッセージ送信の待機時間か</param>
    public void SendMessage(byte[] msg, bool SkipSendMsg = false)
    {
        MsgHandler.instance.IndexingAndStackMsg(msg);

        Debug.LogError("Send Message Now.");
        _AAR.Call("sendMessage", msg);

        if(SkipSendMsg == false)
            StartCoroutine(WaitDelay());
    }

    /// <summary>
    ///メッセージを送信時のディレイが必要な場合に利用するCoroutine関数  
    /// </summary>
    private IEnumerator WaitDelay()
    {
        _CanSendMsg = false;
        _CurrentDelay = SEND_MSG_DELAY;

        while(true)
        {
            _CurrentDelay -= Time.deltaTime;

            if (_CurrentDelay <= 0)
                break;
            else
                yield return null;
        }

        _CanSendMsg = true;
    }

    /// <summary>
    /// AARのメッセージが到着したときに呼び出される関数
    /// </summary>
    public void ReceiveMsg()
    {
        //_MsgSaver.Add(msg.ToString());
        byte[] data = _AAR.Call<byte[]>("ReceiveMsg");
        _MsgSaver.Add(data);

        Debug.LogError("Current Add data : " + PrintMsg(data));
    }

    /// <summary>
    /// Debug用関数です。受けてきたメッセージを出力型Stringに変換してくれる 
    /// </summary>
    /// <param name="msg">出力するメッセージ</param>
    /// <returns></returns>
    private string PrintMsg(byte[] msg)
    {
        string rt = "";

        for (int i = 0; i < msg.Length; ++i)
        {
            rt += "[" + msg[i] + "]";
        }

        return rt;
    }

    /// <summary>
    ///現在、Bluetoothの通信状態に応じて、メッセージを送信する場所を指定 
    /// </summary>
    private void MsgController()
    {
        byte[] data = _MsgSaver[0];
        
        _MsgSaver.RemoveAt(0);

        //メッセージは、通常の順序で含まれてきたのかを確認
        if (MsgHandler.instance.ReceiveIndexCheck(data) == false)
            return;

        switch(_State)
        {
            case COMMUICATOR_STATE.CS_DISCONNECT:
                //どんな状況でも発生してはならない
                break;

            case COMMUICATOR_STATE.CS_NORMAL:
                //一般的な状況。通常の関数に送る
                _Msg.NormalMsgControll(data);
                break;

            case COMMUICATOR_STATE.CS_FAVDAT:
                //名前はお気に入りになっているが、ファイルの転送に関連する関数なので、ファイル転送クラスに送ってくれる
                FileTransferManager.instance.RecvAssembly(data);
                break;

            case COMMUICATOR_STATE.CS_REQUEST_LEAK_STREAM:
                //ファイル転送時に不足しているメッセージを受けてきた
                FileTransferManager.instance.RecvLeakAssemble(data);
                break;

            case COMMUICATOR_STATE.CS_INITIALDAT:
                //最初の本体の状態を受けてきたので、関連する関数で送ってくれる
                _Msg.InitMsgControll(data);
                break;
        }
    }

}
