using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class MainBGScene : MonoBehaviour {

    public static MainBGScene instance;

    AARController _AAR;
    MsgController _Msg; 
    DeviceListScene _List;
    BthCommunicator _BTH;

    bool _IsPowerOn = false;
    bool _IsNormalModeOption = true;
    bool _IsMusicMode = false;

    [SerializeField]
    GameObject _ClickBlock;
    [SerializeField]
    GameObject _AdvClickBlock;
    [SerializeField]
    GameObject _MusicClickBlock;
    [SerializeField]
    GameObject _DisplayGroupObject;
    [SerializeField]
    GameObject _LoadingObject;
    [SerializeField]
    GameObject _TimeToggleClickBlock;

    [SerializeField]
    Transform _DojaGroup;

    List<Text> _DojaTexts = new List<Text>();
    List<Text> _DojaTitles = new List<Text>();

    [SerializeField]
    Text _TempText;
    [SerializeField]
    Text _PatternText;
    [SerializeField]
    Text _MasVolumeText;
    [SerializeField]
    Text _TimerText;
    [SerializeField]
    Text _NormalAdvancedBtnText;


    [SerializeField]
    ToggleGroup _NormalModeGroup;

     
    bool _NoRadioGroupSendMsg = false;


    void Awake () {
        instance = this;
	}

    public void MainBGSceneInit(AARController aar, MsgController msg, DeviceListScene list, BthCommunicator bth)
    {
        _ClickBlock.SetActive(false);

        _AAR = aar;
        _Msg = msg;
        _List = list;
        _BTH = bth;

        _DisplayGroupObject.SetActive(false);
        _LoadingObject.SetActive(false);

        int iNum = _DojaGroup.childCount;
        Transform obj;
        for(int i = 0; i < iNum; ++i)
        {
            obj = _DojaGroup.GetChild(i);
            _DojaTexts.Add(obj.Find("LvText").GetComponent<Text>());
            _DojaTitles.Add(obj.Find("DojaName").GetComponent<Text>());
        }
    }

    #region First Initialize & Control via Client
    /// <summary>
    /// 本体の最初の情報を受けて来るのを待っている
    /// </summary>
    public void FirstValueReady()
    {
        _ClickBlock.SetActive(true);
        _LoadingObject.SetActive(true);
        _DisplayGroupObject.SetActive(false);
    }

    /// <summary>
    /// 最初の情報を受けて、同期させた後に、メイン機能を活性化させる
    /// </summary>
    public void ReleaseFirstValue()
    {
        _ClickBlock.SetActive(false);
        _LoadingObject.SetActive(false);
    }

    /// <summary>
    /// 本体から渡された最初の情報を適用させる
    /// </summary>
    /// <param name="data">本体で越えてきた最初の情報</param>
    public void FirstInitValue(byte[] data)
    {
        //dataを分解して値を位置させてくれる
        Debug.LogError("Data type : " + (MsgController.DATA_TYPE)data[4]);

        //直接入ってもされるが煩わしく、このように表記する理由は、
        //今後変更が行われたとき、これを変更するだけ動作するようにするためである

        _DisplayGroupObject.SetActive(true);

        DojaStringAnalysis(data);
        TemperatureLv(data[18]);
        TimerSet(data[19]);
        Debug.LogError("Mode : " + data[20] + " , " + data[21] + " , " + data[22]);
        PatternStringAnalysis(data[20], data[21], data[22]);
        MasterVolumeSet(data[24]); 

        _LoadingObject.SetActive(false);
        _ClickBlock.SetActive(false);
        _List.gameObject.SetActive(false);

    }

    /// <summary>
    /// パッドに強度を適用する
    /// </summary>
    /// <param name="data">すべてのパッドの強度資料</param>
    void DojaStringAnalysis(byte[] data)
    {
        Debug.LogError(data.Length);
        for(int i = 0; i < 8; i++)
        {
            DojaLv(i, data[i + 10]);
        }
    }

    /// <summary>
    /// 現在の再生モードを適用する
    /// </summary>
    /// <param name="v1">モード分類（大）</param>
    /// <param name="v2">モード分類（詳細）</param>
    /// <param name="v3">パターンナンバー</param>
    void PatternStringAnalysis(int v1, int v2, int v3)
    {
        if (v1 == 3)
        {
            //音楽モード 
            if (_IsMusicMode == false)
                NormalMusicToggle(v1);
        }
        else
        {
            //通常/高級
            NormalMusicToggle(v1);          //音楽モードなら、必ずオフになければならない
            NormalAdvancedToggle(v1);
            if(v1 == 1)
                NormalRadioSet(v2);
            PatternSet(v3);
        }
    }
    #endregion

    #region Graphical Change
    /// <summary>
    /// 特定のパッドの強度テキストを変更する
    /// </summary>
    /// <param name="port">パッドナンバー</param>
    /// <param name="value">強度</param>
    public void DojaLv(int port, int value)
    {
        _DojaTexts[port].text = value.ToString();
    }

    /// <summary>
    /// 現在の温度レベルのテキスト変更
    /// </summary>
    /// <param name="value">変更される温度レベル</param>
    public void TemperatureLv(int value)
    {
        _TempText.text = value.ToString();
    }

    /// <summary>
    /// タイマーのテキストを更新してくれる
    /// </summary>
    /// <param name="value">タイマー時間</param>
    public void TimerSet(int value)
    {
        if (value != 255)
            _TimerText.text = value.ToString();
        else
            _TimerText.text = "연속";
    }

    /// <summary>
    /// 電流パターンナンバーテキスト更新
    /// </summary>
    /// <param name="value">電流パターンナンバー</param>
    public void PatternSet(int value)
    {
        _PatternText.text = value.ToString();
    }

    /// <summary>
    /// マスターボリュームのテキスト更新
    /// </summary>
    /// <param name="value">マスターボリューム</param>
    public void MasterVolumeSet(int value)
    {
        _MasVolumeText.text = value.ToString();
    }

    /// <summary>
    /// ラジオボタンを押さない状態でのデフォルトモードの詳細モードを変更する
    /// </summary>
    /// <param name="index">変更されるデフォルトモードのモードインデックス</param>
    public void NormalRadioSet(int index)
    {
        //ある種のCritical Sectionを設定してくれている
        _NoRadioGroupSendMsg = true;
        Toggle tg = _NormalModeGroup.transform.GetChild(index - 1).gameObject.GetComponent<Toggle>();
        tg.isOn = true;
        _NormalModeGroup.NotifyToggleOn(tg);
        _NoRadioGroupSendMsg = false;
    }

    /// <summary>
    /// デフォルト/詳細モードトグル
    /// </summary>
    /// <param name="iMode">デフォルト/詳細モード</param>
    public void NormalAdvancedToggle(int iMode)
    {
        if (_IsMusicMode == true)
            return;

        if(iMode == 1)
        {
            //Normal
            _NormalAdvancedBtnText.text = "고급";
            _AdvClickBlock.SetActive(false);
            _IsNormalModeOption = true;
        }
        else if(iMode == 2)
        {
            //Advanced
            _NormalAdvancedBtnText.text = "일반";
            _AdvClickBlock.SetActive(true);
            _IsNormalModeOption = false;
        }
    }

    /// <summary>
    /// デフォルト/音楽モードトグル
    /// </summary>
    /// <param name="iMode">デフォルト/音楽モード</param>
    public void NormalMusicToggle(int iMode)
    {
        if(iMode == 3)
        {
            _MusicClickBlock.SetActive(false);
            _AdvClickBlock.SetActive(true);
            _IsMusicMode = true;
        }
        else
        {
            NormalAdvancedToggle(iMode);
            _MusicClickBlock.SetActive(true);
            _AdvClickBlock.SetActive(false);
            _IsMusicMode = false;
        }
    }

    /// <summary>
    /// メッセージを送信するためにパッドを把握する
    /// </summary>
    /// <param name="port">パッドのポート番号</param>
    /// <returns>パッドのメッセージ形式</returns>
    public MsgController.DATA_ACTION DojaPartReturn(int port)
    {
        switch (port)
        {
            case 0:
                return MsgController.DATA_ACTION.DA_PULSE_0;
            case 1:
                return MsgController.DATA_ACTION.DA_PULSE_1;
            case 2:
                return MsgController.DATA_ACTION.DA_PULSE_2;
            case 3:
                return MsgController.DATA_ACTION.DA_PULSE_3;
            case 4:
                return MsgController.DATA_ACTION.DA_PULSE_4;
            case 5:
                return MsgController.DATA_ACTION.DA_PULSE_5;
            case 6:
                return MsgController.DATA_ACTION.DA_PULSE_6;
            case 7:
                return MsgController.DATA_ACTION.DA_PULSE_7;
            default:
                return MsgController.DATA_ACTION.DA_NO_REQUIRE_ACTION;
        }
    }

    /// <summary>
    /// ラジオボタンの重複入力を防止するための制限時間Coroutine
    /// </summary>
    /// <param name="Duration">制限時間（基本0.5秒）</param>
    private IEnumerator TimeClickBlockToggle(float Duration = 0.5f)
    {
        _TimeToggleClickBlock.SetActive(true);
        
        while(Duration > 0)
        {
            Duration -= Time.deltaTime;
            yield return null;
        }


        _TimeToggleClickBlock.SetActive(false);
    }
    #endregion

    #region UI Value Control
    /*
     * 基本的には、以下の関数は、現在のアプリケーションで操作された値を本体に送ってくれる役割をする
     * したがって、すべての関数は、メッセージ送信にのみされており、実際のUIの変化に関する関数は別にある
     */

    /// <summary>
    /// デフォルトモードラジオボタンを押したとき（メッセージ）
    /// </summary>
    /// <param name="index">ラジオボタンのIndex</param>
    public void NormalRadioSetMessageSend(int index)
    {
        if (_NoRadioGroupSendMsg == true)
            return;
        byte[] msg = new byte[1010];
        msg[0] = (byte)index;

        MsgHandler.instance.GenerateMsg(MsgController.DATA_TYPE.DT_MODE_TOGGLE,
            MsgController.DATA_ACTION.DA_NO_REQUIRE_ACTION, msg);

        StartCoroutine(TimeClickBlockToggle());
    }

    /// <summary>
    /// 特定のパッドの電流の強さの変化（メッセージ）
    /// </summary>
    /// <param name="port">パッドのポート</param>
    /// <param name="isUp">立ち上がり/立ち下がり</param>
    public void DojaLv(int port, bool isUp)
    {
        byte[] msg = new byte[1010];

        msg[0] = (byte)DojaPartReturn(port);
        msg[1] = (byte)(isUp == true ? MsgController.DATA_ACTION.DA_UP : MsgController.DATA_ACTION.DA_DOWN);

        MsgHandler.instance.GenerateMsg(MsgController.DATA_TYPE.DT_DOJA,
            MsgController.DATA_ACTION.DA_NO_REQUIRE_ACTION, msg);
    }

    /// <summary>
    /// 温度変化（メッセージ）
    /// </summary>
    /// <param name="isUp">立ち上がり/立ち下がり</param>
    public void TemperatureLv(bool isUp)
    {
        MsgHandler.instance.GenerateMsg(MsgController.DATA_TYPE.DT_TMP,
            isUp == true ? MsgController.DATA_ACTION.DA_UP : MsgController.DATA_ACTION.DA_DOWN, null);
    }

    /// <summary>
    /// タイマーの変化（メッセージ）
    /// </summary>
    /// <param name="baseValue">変化値</param>
    /// <param name="isUp">立ち上がり/立ち下がり</param>
    public void TimerSet(int baseValue, bool isUp)
    {
        byte[] msg = new byte[1010];
        msg[0] = (byte)baseValue;

        MsgHandler.instance.GenerateMsg(MsgController.DATA_TYPE.DT_TMR,
            isUp == true ? MsgController.DATA_ACTION.DA_UP : MsgController.DATA_ACTION.DA_DOWN, msg);
    }

    /// <summary>
    /// 電流再生のOn / Off（メッセージ）
    /// </summary>
    public void PowerOnOff()
    {
        MsgHandler.instance.GenerateMsg(MsgController.DATA_TYPE.DT_PWR,
            MsgController.DATA_ACTION.DA_NO_REQUIRE_ACTION, null);
    }

    /// <summary>
    /// 電流再生パターンの変化（メッセージ）
    /// </summary>
    /// <param name="isUp">立ち上がり/立ち下がり</param>
    public void ModeIndexCount(bool isUp)
    {
        MsgHandler.instance.GenerateMsg(MsgController.DATA_TYPE.DT_PTR,
            isUp == true ? MsgController.DATA_ACTION.DA_UP : MsgController.DATA_ACTION.DA_DOWN,
            null);
    }

    /// <summary>
    /// デフォルト/詳細モード変換（メッセージ）
    /// </summary>
    public void NormalAdvancedToggle()
    {
        if (_IsMusicMode == true)
            return;
        MsgHandler.instance.GenerateMsg(MsgController.DATA_TYPE.DT_NA_MODE, MsgController.DATA_ACTION.DA_NO_REQUIRE_ACTION, null);
    }

    /// <summary>
    /// 音楽モード変換（メッセージ）
    /// </summary>
    public void MusicModeOn()
    {
        MsgHandler.instance.GenerateMsg(MsgController.DATA_TYPE.DT_NM_MODE, MsgController.DATA_ACTION.DA_NO_REQUIRE_ACTION, null);
    }

    /// <summary>
    /// 音楽ファイルの変換（メッセージ）
    /// </summary>
    /// <param name="isForward">前の再生/次再生</param>
    public void MusicFileForwardbackward(bool isForward)
    {
        MsgHandler.instance.GenerateMsg(MsgController.DATA_TYPE.DT_MUSIC_CHANGE, 
            isForward == true ? MsgController.DATA_ACTION.DA_UP: MsgController.DATA_ACTION.DA_DOWN, null);
    }

    /// <summary>
    /// マスターボリュームの変化（メッセージ）
    /// </summary>
    /// <param name="isUp"></param>
    public void VolumeControl(bool isUp)
    {
        MsgHandler.instance.GenerateMsg(MsgController.DATA_TYPE.DT_OPT_MASV, (isUp == true ? MsgController.DATA_ACTION.DA_UP : MsgController.DATA_ACTION.DA_DOWN), null);
    }

    /// <summary>
    /// メッセージを関連する関数に送信送信を可能にする
    /// </summary>
    /// <param name="msg">内容</param>
    /// <param name="skipDelay">伝送遅延時間を与えるのだろうか</param>
    void sendMsg(byte[] msg, bool skipDelay = false)
    {
        _BTH.SendMessage(msg, skipDelay);
    }
    #endregion
}
