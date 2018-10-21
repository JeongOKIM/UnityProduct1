using UnityEngine;
using System.Collections;
using System.IO;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

public class Fav : MonoBehaviour {

    public static Fav instance;
    RemoteMain _RemoteMain;
    MainBGScene _Main;
    BthCommunicator _Bth;
    AlertManager _Alert;
    

    List<byte[]> _FileAssembleList = new List<byte[]>();
    List<GameObject> _ListObject = new List<GameObject>();
    int _FileLength;
    int _NumOfAssembly;
    int _NumOfRecvAssembly;
    int _RemainByte;
    FileStream _FileStream;

    [SerializeField]
    GameObject _ClickBlock;
    [SerializeField]
    Transform _Content;
    [SerializeField]
    GameObject _Prefabs;

    [SerializeField]
    GameObject _UserTypes;

    [SerializeField]
    GameObject _Save;
    [SerializeField]
    GameObject _Load;

    Vector3 _CellDefaultPos = new Vector3(266.5f, -50f);
    Vector3 _CellScale = new Vector3(1f, 1f, 1f);

    int _CurrentLeakPosition;
    bool _LeakMsgCome;
    bool _RecheckStatus = false;
    int _NumOfFailedResend = 100;

    public bool LoadPopupOn { get { return _Load.activeSelf; } }

    /// <summary>
    /// ユーザが保存した機器の動作情報を保存した構造体
    /// バイナリ形式で本体に保存され、ここで本体に存在するファイルを転送受け解析し、保存する 
    /// </summary>
    public struct FavData
    {
        int _UserType;
        int _Temp;
        List<int> _DojaLv;
        int _Pattern;
        public int Pattern { get { return _Pattern; } }
        int _LastPattern;
        public void Init()
        {
            _DojaLv = new List<int>();
        }

        /// <summary>
        /// 一般的な方法のデータセット
        /// </summary>
        /// <param name="type"></param>
        /// <param name="temp"></param>
        /// <param name="DojaLvArr"></param>
        /// <param name="pattern"></param>
        /// <param name="lastPattern"></param>
        public void DataSet(int type, int temp, int[] DojaLvArr,
            int pattern, int lastPattern)
        {
            _UserType = type;
            _Temp = temp;
            _DojaLv.Clear();
            _DojaLv.AddRange(DojaLvArr);
            _Pattern = pattern;
            _LastPattern = lastPattern;
        }

        /// <summary>
        /// ファイルからインポートするときのデータセット
        /// </summary>
        /// <param name="reader"></param>
        public void DataLoadTransfer(ref System.IO.BinaryReader reader)
        {
            _UserType = reader.ReadInt32();
            _Temp = reader.ReadInt32();
            for (int i = 0; i < 8; ++i)
            {
                int temp = reader.ReadInt32();
                _DojaLv.Add(temp);
            }
            _Pattern = reader.ReadInt32();
            _LastPattern = reader.ReadInt32();
        }

        /// <summary>
        /// 保存されたデータを指定されたフォームで画面に表示する際に、受信関数
        /// </summary>
        /// <param name="data"></param>
        /// <param name="index"></param>
        public void DataDisplayTransfer(GameObject data, int index)
        {
            Text idx = data.transform.Find("Index").GetComponent<Text>();
            Text lv = data.transform.Find("Lv").GetComponent<Text>();
            Text upt = data.transform.Find("User_Pattern_Temp").GetComponent<Text>();

            idx.text = index.ToString();

            string lvString = "";
            for(int i = 0; i < _DojaLv.Count; ++i)
            {
                lvString += _DojaLv[i].ToString();
                if(i != _DojaLv.Count - 1)
                {
                    lvString += " | ";
                }
            }
            lv.text = lvString;

            upt.text = ReturnUserType() + " | " + ReturnPatternType() + " | " + ReturnTempLv();
        }

        /// <summary>
        /// Usertypeによるテキストを読んでくる関数です。本体では、言語設定に応じた値を出力しますがここはまだ言語の設定に関するシステムが構築されていないため、韓国語をReturnする
        /// </summary>
        /// <returns></returns>
        string ReturnUserType()
        {
            switch (_UserType)
            {
                case 0:
                    return "유저";
                case 1:
                    return "아버지";
                case 2:
                    return "할아버지";
                case 3:
                    return "할머니";
                case 4:
                    return "어머니";
                default:
                    return "알 수 없음";
            }
        }

        /// <summary>
        /// パターンの動作状態に応じたテキスト値を出力する。本体では、言語設定に応じたテキストを出力しますが、ここでは、言語の設定システムが構築されていないため、韓国語をReturnする
        /// </summary>
        /// <returns></returns>
        string ReturnPatternType()
        {
            switch (_Pattern)
            {
                case 1:
                    return "자동모드1";
                case 2:
                    return "자동모드2";
                case 3:
                    return "자동모드3";
                case 4:
                    return "자동모드4";
                case 5:
                    return "자동모드5";
                case 6:
                    return "랜덤모드";
                case 7:
                    return "고정모드";
                case 8:
                    return "고급모드";
                case 9:
                    return "음악모드";
                default:
                    return "자동모드1";
            }
        }

        /// <summary>
        /// 温度レベルのテキストを設定してくれる。言語パックに応じた設定が存在するが、ここではないので、韓国語を出力する
        /// </summary>
        /// <returns></returns>
        string ReturnTempLv()
        {
            return "온도 " + _Temp.ToString() + "단계";
        }


    }

    List<FavData> _FavDataList = new List<FavData>();


    void Awake()
    {
        instance = this;
    } 

    void OnDestroy()
    {
        //送信された本体のお気に入り情報をアプリケーション終了時に廃棄することができるようしてくれる
        FileInfo fi = new FileInfo(Application.persistentDataPath + "/temp.bin");
        if(fi != null)
        {
            try
            {
                fi.Delete();
            } 
            catch(Exception e)
            {
                Debug.LogError(e);
            }
        }
    }

    public void FavInit(RemoteMain rmain, MainBGScene main, BthCommunicator bth,
        AlertManager alert)
    {
        _RemoteMain = rmain;
        _Main = main;
        _Bth = bth;
        _Alert = alert;

        #region Ptn Save Buttons add Listner
        _Save.SetActive(true);

        //Buttonは6個存在するが、index5に対応するボタンは、Exitボタンで一時的に使用する予定である
        for (int i = 0; i < 5; ++i)
        {
            AddSaveListener(_UserTypes.transform.GetChild(i).GetComponent<Button>(), i);
        }

        _Save.SetActive(false);
        #endregion
    }

    /// <summary>
    /// お気に入りセーブ途中Disconnectが起きた時、着信関数
    /// </summary>
    public void FavDisconnect()
    {
        _ClickBlock.SetActive(false);
        _Load.SetActive(false);
    }

    #region Save Function
    /// <summary>
    /// お気に入り保存ポップアップ実行
    /// </summary>
    public void InitSave()
    {
        _Save.SetActive(true);
        _ClickBlock.SetActive(true);
    }
    /// <summary>
    /// お気に入り保存ポップアップウィンドウを解除
    /// </summary>
    public void ExitFavSavePopup()
    {
        _Save.SetActive(false);
        _ClickBlock.SetActive(false);
    }

    /// <summary>
    /// お気に入りの保存を実行する（本体にメッセージを送信）
    /// </summary>
    /// <param name="type">代表画像</param>
    public void SendSaveMessage(int type)
    {
        //ここで保存ボタンを押してくれるも、本体からの情報を格納するための関連メッセージを本体に送信してくれる
        byte[] msg = new byte[1010];
        msg[0] = (byte)type;

        MsgHandler.instance.GenerateMsg(MsgController.DATA_TYPE.DT_FAV_SAVE,
            MsgController.DATA_ACTION.DA_NO_REQUIRE_ACTION, msg);

        _Bth.SendMessage(msg);
        _Save.SetActive(false);
        _ClickBlock.SetActive(false);
    }

    /// <summary>
    /// 各ボタンにイベントを設定
    /// </summary>
    /// <param name="btn">イベントを設定するボタン</param>
    /// <param name="index">代表画面インデックス</param>
    private void AddSaveListener(Button btn, int index)
    {
        btn.onClick.AddListener(() => UIActor.instance.FavSave(index));
    }
    #endregion

    #region Load Function

    /// <summary>
    /// （UI正常出力テスト用コード）お気に入りのロード最初の実行
    /// </summary>
    public void InitLoadTest()
    {
        _Load.SetActive(true);

        GameObject cell;
        Vector3 pos;

        for (int i = 0; i < 3; ++i)
        {
            cell = Instantiate(_Prefabs) as GameObject;
            cell.transform.SetParent(_Content);
            pos = _CellDefaultPos;

            pos.y -= 90 * i;
            cell.transform.localPosition = pos;
            cell.transform.localScale = _CellScale;

            _ListObject.Add(cell);
            AddLoadListener(cell.GetComponent<Button>(), i);
        }

        //Content サイズ確定
        float contentSize = 700f + (90f * (Mathf.Max(_FavDataList.Count - 6, 0)));

        _Content.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, contentSize);
    }

    /// <summary>
    /// お気に入りロード情報とUIのリストを解除
    /// </summary>
    private void DestroyList()
    {
        for(int i = 0; i < _ListObject.Count; ++i)
        {
            Destroy(_ListObject[i]);
        }

        _ListObject.Clear();
        _FavDataList.Clear();
    }

    /// <summary>
    /// ロードウィンドをオフする
    /// </summary>
    /// <param name="err"></param>
    public void ExitFavLoadPopup(bool err = false)
    {
        _Load.SetActive(false);
        _ClickBlock.SetActive(false);

        DestroyList();

        if(err == true)
            _Alert.ActiveNoticeWithTime("Error Occured. \r\n Please retry.");
    }

    /// <summary>
    /// お気に入りダウンロードのための本体に情報を要求する
    /// </summary>
    public void InitLoad()
    {
        byte[] msg = new byte[1010];
        msg[0] = (byte)MsgController.TF_FILE_TYPE.TF_FAV;
        MsgHandler.instance.GenerateMsg(MsgController.DATA_TYPE.DT_FILE_RQ, MsgController.DATA_ACTION.DA_NO_REQUIRE_ACTION, msg);
    }

    /// <summary>
    /// お気に入りロードを実行する
    /// </summary>
    /// <param name="index">お気に入り情報リストindex</param>
    public void SendLoadMsg(int index)
    {
        byte[] msg = new byte[1010];
        msg[0] = (byte)index;

        MsgHandler.instance.GenerateMsg(MsgController.DATA_TYPE.DT_FAV_LOAD, MsgController.DATA_ACTION.DA_NO_REQUIRE_ACTION, msg);

        _Load.SetActive(false);
        _ClickBlock.SetActive(false);
        DestroyList();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="length"></param>
    /// <param name="loop"></param>
    /// <param name="remain"></param>
    public void ReadyToRecvAssembly(int length, int loop, int remain)
    {
        _ClickBlock.SetActive(true);

        //とにかくどのような理由のためにFが出たら元の状態に還元してくれる
        if (FileTransferManager.instance.ReadyToRecvAssembly(length, loop, remain, MsgController.TF_FILE_TYPE.TF_FAV) == false)
        {
            _ClickBlock.SetActive(false);
            return;
        }
    }

    /// <summary>
    /// お気に入りリストをロードする
    /// </summary>
    /// <param name="reader"></param>
    public void FavListLoad(ref BinaryReader reader)
    {
        CreateJFileAndAnalysis(ref reader);

        CreateDisplayList();

        _Load.SetActive(true);
        _ClickBlock.SetActive(false);
    }

    /// <summary>
    /// 受けてきたファイルから情報を抽出する
    /// </summary>
    /// <param name="reader">受けてきたファイルを読み込むバイナリリーダー</param>
    private void CreateJFileAndAnalysis(ref BinaryReader reader)
    {
        int _iNumOfList = reader.ReadInt32();   //いくつかのデータが保存されているか確認する

        FavData dat;
        for (int i = 0; i < _iNumOfList; ++i)
        {
            dat = new FavData();
            dat.Init();
            dat.DataLoadTransfer(ref reader);
            _FavDataList.Add(dat);
        }
    }

    /// <summary>
    /// 受けてきたファイルから情報を抽出する（旧バージョン）
    /// </summary>
    private void CreateJFileAndAnalysis()
    {
        JFile _File = JFile.CreateBinary("temp.bin", true);

        if (_File != null)
        {
            var reader = _File._binaryReader;
            int _iNumOfList = reader.ReadInt32();   //いくつかのデータが保存されているか確認する

            FavData dat;
            for (int i = 0; i < _iNumOfList; ++i)
            {
                dat = new FavData();
                dat.Init();
                dat.DataLoadTransfer(ref reader);
                _FavDataList.Add(dat);
            }
            _File.Close();
        }
        else
        {
            Debug.LogError("No File Exist. Error Occured");
        }
    }

    /// <summary>
    /// 画面上のボタンのリストにボタンを追加してくれる
    /// </summary>
    private void CreateDisplayList()
    {
        if (_FavDataList.Count <= 0)
        {
            return;
        }

        GameObject cell;
        Vector3 pos;
        for (int i = 0; i < _FavDataList.Count; ++i)
        {
            cell = Instantiate(_Prefabs) as GameObject;
            _FavDataList[i].DataDisplayTransfer(cell, i + 1);
            cell.transform.SetParent(_Content);
            pos = _CellDefaultPos;

            AddLoadListener(cell.GetComponent<Button>(), i);

            pos.y -= 90 * i;
            cell.transform.localPosition = pos;
            cell.transform.localScale = _CellScale;
            
            _ListObject.Add(cell);
        }

        //Content サイズ確定
        float contentSize = 700f + (90f * (Mathf.Max(_FavDataList.Count - 6, 0)));

        _Content.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, contentSize);

        pos = _Content.transform.position;
        pos.y = 0f;
        _Content.transform.position = pos;
    }

    /// <summary>
    /// 各ボタンにクリックイベントを追加してくれる
    /// </summary>
    /// <param name="btn">ボタン</param>
    /// <param name="value">index</param>
    private void AddLoadListener(Button btn, int value)
    {
        btn.onClick.AddListener(() => UIActor.instance.FavSelect(value));
    }


    #endregion
}
