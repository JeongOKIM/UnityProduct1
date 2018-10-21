using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;

/*
    基本的な電流パターンの再生モードを調整したり、（ノーマルモード）/ユーザーが必要に応じて、電流パターンや再生時間を調整する（詳細モード）の部分を制御するクラス
 */
public class PatternActiveMgr : MonoBehaviour
{

    public static PatternActiveMgr instance;

    #region Cell State Parameter
    public enum PTN_STATE
    {
        PTN_AUTO_1 = 1,
        PTN_AUTO_2,
        PTN_AUTO_3,
        PTN_AUTO_4,
        PTN_AUTO_5,
        PTN_RANDOM,
        PTN_STOP,
        PTN_CUSTOM,
        PTN_MUSIC,
    }

    private PTN_STATE _CurrentMoveState = PTN_STATE.PTN_AUTO_1;
    private PTN_STATE _PrevNormalState = PTN_STATE.PTN_AUTO_1;
    public int CurrentState { get { return (int)_CurrentMoveState; } }

    //詳細モードで使用される電流パターンと再生時間を含む構造体
    public struct PTN_CELL
    {
        int _Mode;
        public int Mode { get { return _Mode; } set { _Mode = value; } }
        float _Time;
        public float Time { get { return _Time; } set { _Time = value; } }

        public void SetCell(int mode, float time)
        {
            _Mode = mode;       //Mode Index
            _Time = time;       //Mode total duration(Minute)
        }
    }


    List<PTN_CELL> _CellList = new List<PTN_CELL>();
    public int RemainCell { get { return _CellList.Count; } }
    List<PTN_CELL> _AdvancedCellBackUp = new List<PTN_CELL>();
    List<GameObject> _AdvancedOpCellObjects = new List<GameObject>();

    int _CurrentModeIndex;
    public int CurrentIndex { get { return _CurrentModeIndex; } }
    public int CurrentIndexInCustom
    {
        get
        {
            if (_CurrentMoveState == PTN_STATE.PTN_RANDOM || _CurrentMoveState == PTN_STATE.PTN_CUSTOM)
                return _CellList[_CurrentModeIndex].Mode;
            else
                return _CurrentModeIndex;
        }
    }
    float _RemainTime;
    bool _PlayPatternNow = false;
    bool _RadioCriticalSection = false;
    public bool PlayPattern { get { return _PlayPatternNow; } set { _PlayPatternNow = value; } }
    Coroutine _PlayCo = null;

    //Normal State パターン間隔
    float _NormalStatePatternTick = 3f;
    //Tick 増減単位
    float _PatternTickRate = 0.5f;
    //パターン間隔の最大時間
    float _PatternTimeUpperLimit = 10f;
    //パターンTick増減最大単位
    float _PatternTickRateUpperLimit = 2f;

    //保存されるファイルの名前
    string _PatternTickRelatedFileName = "PatternInfo.ini";


    #endregion
    #region UI Control Parameter

    public List<Text> _RadioRabels = new List<Text>();
    public Text _OptionToggleBtnLabel;
    public Transform _OptionToggleBtnImage;
    public GameObject _NormalPanel;
    public GameObject _AdvancedPanel;
    private bool _IsNormalPtnOption = true;
    public GameObject _CellPrefab;
    public GameObject _Content;
    public GameObject _CellAddButton;
    [SerializeField]
    private GameObject _ModePanel;
    [SerializeField]
    private GameObject _MusicPanel;
    [SerializeField]
    private GameObject _ModeDisplayText;
    [SerializeField]
    private GameObject _AdvancedClickBlock;
    [SerializeField]
    private ToggleGroup _NormalPanelToggleGroup;
    [SerializeField]
    private List<Toggle> _NormalPanelToggles;
    [SerializeField]
    private ToggleGroup _NormalMusicToggleGroup;
    [SerializeField]
    private List<Toggle> _NormalMusicToggles;
    [SerializeField]
    private Text _NormalPatternPlayTimeText;
    [SerializeField]
    private Slider _PatternPlayTimeSlide;

    bool _Initialize = false;
    bool _RndToggleInput = false;
    int _CurrentPlayMode = -1;
    bool _PrevCustomModeOn = false;

    int _FavLastIndex = -1;
    public int FavLastIndex { set { _FavLastIndex = value; } }
    private int _CellListMove = -51;

    private bool _StopChangeModeAtBtn = false;

    #endregion


    // Use this for initialization
    void Awake()
    {
        instance = this;
        TickStateLoad();
        _PatternPlayTimeSlide.maxValue = _PatternTimeUpperLimit;
        _PatternPlayTimeSlide.minValue = _PatternTickRate;
        _PatternPlayTimeSlide.value = _NormalStatePatternTick;
        PatternPlayTimeTextChange();
        Set_ModeDisplayText(1);
        InitializeListSetting();
    }

    void Start()
    {
        Labeling();

        var tmpMusicPanal = _MusicPanel.GetComponent<MusicPanal>();
        tmpMusicPanal.Init();
    }


    #region Cell List Creation
    /// <summary>
    /// 詳細モードでは、ユーザーが一つの電流パターンを追加するときにリストに追加されるオブジェクトを生成
    /// </summary>
    public void AddCell()
    {
        PTN_CELL newCell = new PTN_CELL();
        newCell.SetCell(1, 1f);
        _CellList.Add(newCell);

        GameObject newCellUI = Instantiate(_CellPrefab) as GameObject;
        newCellUI.transform.GetComponent<PatternCell>().Setup(_CellList.Count - 1, 1, 1);
        _AdvancedOpCellObjects.Add(newCellUI);

        //Front Number text change
        Transform cellTrans = newCellUI.transform;
        Text txt = cellTrans.Find("FrontNumber").GetComponent<Text>();
        txt.text = _CellList.Count.ToString();

        //Position Change
        cellTrans.SetParent(_Content.transform);

        Vector3 pos = new Vector3(135, 15);

        pos.y += _CellListMove * (_CellList.Count - 1);

        cellTrans.localPosition = pos;
        cellTrans.localScale = new Vector3(0.85f, 1);

        //Contentのサイズを調整する
        var _ContentRT = _Content.GetComponent<RectTransform>();
        float contentSize = 51 * (_CellList.Count + 1);
        if (contentSize <= 350f)
        {
            contentSize = 350f;
        }
        _ContentRT.sizeDelta = new Vector2(0f, contentSize);

        //Contentの位置を調整する
        {
            Vector2 tmpVec = _ContentRT.anchoredPosition;
            tmpVec.y = Mathf.Max(contentSize - 350f, 0f);
            _ContentRT.anchoredPosition = tmpVec;
        }

        pos = new Vector3(135, 30);
        pos.y += _CellListMove * (_CellList.Count + 1);
        _CellAddButton.transform.localPosition = pos;
    }

    /// <summary>
    /// 詳細モードのパターンオブジェクトの情報が変更されるときに呼び出される関数
    /// </summary>
    /// <param name="index">リストindex</param>
    /// <param name="mode">変更モード</param>
    /// <param name="time">変更時間</param>
    public void ChangeCellWithIndex(int index, int mode, float time)
    {
        PTN_CELL cell = _CellList[index];
        cell.Mode = mode;
        cell.Time = time;
        _CellList[index] = cell;
    }

    /// <summary>
    /// リストの情報をindexを参照して削除
    /// </summary>
    /// <param name="index">削除されるindex</param>
    public void DeleteCellWithIndex(int index)
    {
        _CellList.RemoveAt(index);
        GameObject dtObj = _AdvancedOpCellObjects[index];
        _AdvancedOpCellObjects.RemoveAt(index);
        Destroy(dtObj);
        Vector3 pos;
        Text txt;
        for (int i = 0; i < _AdvancedOpCellObjects.Count; ++i)
        {
            if (_AdvancedOpCellObjects[i] != null)
            {
                pos = new Vector3(135, 15);
                txt = _AdvancedOpCellObjects[i].transform.GetChild(0).GetComponent<Text>();
                txt.text = (i + 1).ToString();
                _AdvancedOpCellObjects[i].GetComponent<PatternCell>().IndexChange(i);

                pos.y += _CellListMove * i;

                _AdvancedOpCellObjects[i].transform.localPosition = pos;
                _AdvancedOpCellObjects[i].transform.localScale = new Vector3(0.85f, 1);
            }
        }

        //Content의 크기를 조정한다
        float contentSize = 51 * (_CellList.Count + 1);
        if (contentSize <= 350f)
        {
            contentSize = 350f;
        }

        _Content.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, contentSize);

        pos = new Vector3(135, 30);
        pos.y += _CellListMove * (_CellList.Count + 1);
        _CellAddButton.transform.localPosition = pos;

        if (index == 0)
        {
            Set_ModeDisplayText(_CellList[0].Mode);
        }
    }

    /// <summary>
    /// リストの一番最後の情報を削除する
    /// </summary>
    public void DeleteCell()
    {
        _CellList.RemoveAt(_CellList.Count - 1);
    }
    #endregion

    #region Mode List Control Function
    /// <summary>
    /// パターン再生リストを作成してくれる
    /// </summary>
    /// <param name="fromFav">お気に入りロードから実行されたかのかどうか</param>
    public void ListSetting(bool fromFav = false)
    {
        if (_IsNormalPtnOption == false)    //必ず詳細モードである
        {
            Debug.LogError("Custom Mode Yet");
            return;
        }

        //保存されていたデータが入ってくる場合、すでにLast Indexを含むセッティングがされているので、
        //再びセッティングされてLast Indexが初期化されるのを防ぐために、あえて新たくれる
        if (fromFav == false)
        {
            _CellList.Clear();
            _AdvancedOpCellObjects.Clear();

            switch (_CurrentMoveState)
            {
                case PTN_STATE.PTN_RANDOM:
                    RandomModeSet();
                    _RndToggleInput = true;
                    break;
                case PTN_STATE.PTN_CUSTOM:      
                    _RndToggleInput = false;
                    Debug.LogError("Custom Mode Yet");
                    break;
                case PTN_STATE.PTN_STOP:
                    //固定パターンの場合は、前のIndexを受けて来なければならすることが、現在使用中のパターンを持って来て固定するので
                    if (_FavLastIndex == -1 && _CellList.Count > 0)
                        _FavLastIndex = _CellList[_CurrentModeIndex].Mode;
                        AutoModeSet(PTN_STATE.PTN_AUTO_1);      //今では、固定モードのとき、すべてのモードをサポートするようにしてくれる。
                                                                //もし設定が変更される場合、それに合わせてコードを修正してくれるようしよう
                    break;
                default:
                    AutoModeSet(_CurrentMoveState);
                    _RndToggleInput = false;
                    break;
            }
        }
        else
        {
            //今では、固定モードのとき、すべてのモードをサポートするようにしてくれる。
            if (_CurrentMoveState == PTN_STATE.PTN_STOP)
                AutoModeSet(PTN_STATE.PTN_AUTO_1);

            //
            if (_CurrentMoveState == PTN_STATE.PTN_RANDOM)
                _RndToggleInput = true;
            else
                _RndToggleInput = false;
        }

        _Initialize = true;

    }

    /// <summary>
    /// 指定されたデフォルトのモードを設定してくれる関数
    /// </summary>
    /// <param name="setState">変更されるデフォルトのモード</param>
    /// <param name="LastPatternNumber">最後に再生されたパターンナンバー</param>
    public void AutoModeSet(PTN_STATE setState, int LastPatternNumber = 0)
    {
        if (setState == PTN_STATE.PTN_RANDOM
            || setState == PTN_STATE.PTN_CUSTOM)
            return;

        _CellList.Clear();
        int iStart = 0;
        int iNum = 0;
        switch (setState)
        {
            case PTN_STATE.PTN_AUTO_1:
                iStart = 1;
                iNum = 40;
                break;
            case PTN_STATE.PTN_AUTO_2:
                iStart = 1;
                iNum = 10;
                break;
            case PTN_STATE.PTN_AUTO_3:
                iStart = 11;
                iNum = 10;
                break;
            case PTN_STATE.PTN_AUTO_4:
                iStart = 21;
                iNum = 10;
                break;
            case PTN_STATE.PTN_AUTO_5:
                iStart = 31;
                iNum = 10;
                break;
        }

        PTN_CELL ptn;

        for (int i = 0; i < iNum; ++i)
        {
            ptn = new PTN_CELL();
            ptn.SetCell(iStart, _NormalStatePatternTick);     
            _CellList.Add(ptn);
            ++iStart;
        }

        if (LastPatternNumber > 0)
        {
            _CurrentModeIndex = LastPatternNumber;
        }
        else
        {
            if (_FavLastIndex > 0)
            {
                _CurrentModeIndex = _FavLastIndex;
                _FavLastIndex = -1;
            }
            else
                _CurrentModeIndex = 0;
        }
    }
    /// <summary>
    /// 基本モードでの電流パターン再生時間を変更する関数
    /// </summary>
    /// <param name="isUP">時間が増加するかどうか</param>
    public void NormalTimeTickChange(bool isUP)
    {
        if (isUP)
        {
            if (_NormalStatePatternTick < _PatternTimeUpperLimit)
                _NormalStatePatternTick += _PatternTickRate;
        }
        else
        {
            if (_NormalStatePatternTick > 30)   //再生時間は最大30分を越すない
                _NormalStatePatternTick -= _PatternTickRate;
        }


        TickStateSave();
        PatternPlayTimeTextChange();
        AcceptTimeTickChange();     //おそらく今後は移さだろうが、現在はここに置くようにしましょう
    }

    /// <summary>
    /// 基本モードでの電流パターン再生時間を変更する関数
    /// </summary>
    /// <param name="time">いくつかの単位で変更をしてくれるのか</param>
    /// <param name="isUp">時間が増加するかどうか</param>
    public void TickRateChange(float time, bool isUp)
    {
        if (isUp)
        {
            if (_PatternTickRate + time <= _PatternTickRateUpperLimit)
                _PatternTickRate += time;
            else
                _PatternTickRate = _PatternTickRateUpperLimit;
        }
        else
        {
            if (_PatternTickRate - time > 30)
                _PatternTickRate -= time;
            else
                _PatternTickRate = 30;
        }

        TickStateSave();
    }

    /// <summary>
    /// 基本モードでの電流パターン再生時間をスライダーを使用して変更する関数
    /// </summary>
    public void TickRateSlideChange()
    {
        float tempRemain = _PatternPlayTimeSlide.value % _PatternTickRate;
        float tempValue = _PatternPlayTimeSlide.value - tempRemain;

        if (tempRemain > _PatternTickRate / 2)
        {
            tempValue += _PatternTickRate;
        }

        _NormalStatePatternTick = tempValue;
        _PatternPlayTimeSlide.value = _NormalStatePatternTick;

        TickStateSave();
        PatternPlayTimeTextChange();
        AcceptTimeTickChange();     //おそらく今後は移さだろうが、現在はここに置くようにしましょう
    }

    /// <summary>
    /// 変更された再生時間を適用させる関数
    /// </summary>
    public void AcceptTimeTickChange()
    {
        if(_CurrentMoveState != PTN_STATE.PTN_CUSTOM && _CurrentMoveState != PTN_STATE.PTN_MUSIC
            && _CurrentMoveState != PTN_STATE.PTN_STOP)
        {
            if(_CurrentMoveState == PTN_STATE.PTN_RANDOM)
            {
                RandomModeSet(_CellList[_CurrentModeIndex].Mode);
            }
            else
            {
                AutoModeSet(_CurrentMoveState, _CurrentModeIndex);
            }
        }
    }

    /// <summary>
    /// 現在の再生オプションを保存する
    /// </summary>
    public void TickStateSave()
    {
        //すべてのデータを一度に保存する
        JFile _File;
        _File = JFile.CreateBinary(_PatternTickRelatedFileName, false);
        if (_File == null)
        {
            //例外処理
        }
        else
        {
         
            var writer = _File._binaryWriter;
            writer.Write(_NormalStatePatternTick);
            writer.Write(_PatternTickRate);

            _File.Close();
        }
    }

    /// <summary>
    /// 保存されている再生オプションをロードする
    /// </summary>
    public void TickStateLoad()
    {
        JFile _File;
        _File = JFile.CreateBinary(_PatternTickRelatedFileName, true);
        if (_File != null)
        {
            
            try
            {
                var reader = _File._binaryReader;
                _NormalStatePatternTick = reader.ReadSingle();
                _PatternTickRate = reader.ReadSingle();
                _File.Close();
            }
            catch //(Exception e)
            {
                _File.Close();
                File.Delete(_PatternTickRelatedFileName);
            }
        }
        else
        {
            //デフォルトでは、ファイルを一つにしてくれる
            TickStateSave();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    private void PatternPlayTimeTextChange()
    {
        float second = 0;
        string secondString = "";
        second = (_NormalStatePatternTick - (int)_NormalStatePatternTick) * 60;
        if (second > 0)
            secondString += " " + second + "초"; 
        
        _NormalPatternPlayTimeText.text = (int)_NormalStatePatternTick + "분" + secondString;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="FirstNumber"></param>
    public void RandomModeSet(int FirstNumber = 0)
    {
        if (FirstNumber == 0)
        {
            if (_CurrentMoveState != PTN_STATE.PTN_RANDOM)
                return;
        }
        else
        {
            //First Number가 0이 넘어서 들어오는 경우는 반드시 Random모드로 들어오도록 한다
            _CurrentMoveState = PTN_STATE.PTN_RANDOM;
            _FavLastIndex = -1;
        }

        //TextOutLogMgr.Instance.LogError("RandomModeSet");
        _CellList.Clear();

        List<int> tempList = new List<int>();
        for (int i = 1; i <= PulseMain.instance.MaxPatternLv; ++i)
        {
            tempList.Add(i);
        }

        int iSet = 0;
        int iStartPoint = 1;
        PTN_CELL ptn;

        if(FirstNumber != 0)
        {
            //TextOutLogMgr.Instance.LogError("FirstNumber : " + FirstNumber);
            tempList.RemoveAt(FirstNumber - 1);
            ptn = new PTN_CELL();
            ptn.SetCell(FirstNumber, _NormalStatePatternTick); 
            _CellList.Add(ptn);
            ++iStartPoint;
        }
        else if(_FavLastIndex > 0)
        {
            tempList.RemoveAt(_FavLastIndex - 1);
            ptn = new PTN_CELL();
            ptn.SetCell(_FavLastIndex, _NormalStatePatternTick);
            _CellList.Add(ptn);
            ++iStartPoint;

            _FavLastIndex = -1;
        }

        for (int i = iStartPoint; i <= PulseMain.instance.MaxPatternLv; ++i)
        {
            iSet = UnityEngine.Random.Range(0, tempList.Count);
            ptn = new PTN_CELL();
            ptn.SetCell(tempList[iSet], _NormalStatePatternTick);

            _CellList.Add(ptn);
            tempList.RemoveAt(iSet);
        }

        _CurrentModeIndex = 0;
    }
    #endregion

    #region UI Initialize

    /// <summary>
    /// プログラム区同時に関連テキストを設定してくれる。言語に応じて取得できるように駆動時付ける形とする
    /// </summary>
    private void Labeling()
    {
        FontSystem fSystem = FontSystem.instance;
        //Toggle
        for (int i = 0; i < _RadioRabels.Count; ++i)
        {
            _RadioRabels[i].text = fSystem.DataTransfer(FontSystem.FONT_ID.FID_MODES_AUTO1 + i);
        }

        //Nor/Mus option panel toggle
        _NormalMusicToggles[1].transform.Find("Text").GetComponent<Text>().text = fSystem.DataTransfer(FontSystem.FONT_ID.FID_BT_NORMAL);
        _NormalMusicToggles[0].transform.Find("Text").GetComponent<Text>().text = fSystem.DataTransfer(FontSystem.FONT_ID.FID_BT_MUSIC);

        //Nor/Adv option panel toggle
        _OptionToggleBtnLabel.text = fSystem.DataTransfer(FontSystem.FONT_ID.FID_BT_ADVOPTION);
    }

    /// <summary>
    /// 基本モードでは、詳細モードに切り替えたときに、現在の保存されているデータがある場合はリストを作成してくれる
    /// </summary>
    private void AdvancedListLoad()
    {
        GameObject obj;
        for (int i = 0; i < _AdvancedOpCellObjects.Count; ++i)
        {
            obj = _AdvancedOpCellObjects[i];
            _AdvancedOpCellObjects[i] = null;
            Destroy(obj);
        }
        _AdvancedOpCellObjects.Clear();

        Vector3 pos;
        for (int i = 0; i < _CellList.Count; ++i)
        {
            GameObject newCellUI = Instantiate(_CellPrefab) as GameObject;
            newCellUI.transform.GetComponent<PatternCell>().Load(i, _CellList[i].Mode, (int)_CellList[i].Time);
            _AdvancedOpCellObjects.Add(newCellUI);

            //Front Number text change
            Transform cellTrans = newCellUI.transform;
            Text txt = cellTrans.Find("FrontNumber").GetComponent<Text>();
            txt.text = (i + 1).ToString();

            //Position Change
            cellTrans.SetParent(_Content.transform);

            pos = new Vector3(135, 15);

            pos.y += _CellListMove * (i);

            cellTrans.localPosition = pos;
            cellTrans.localScale = new Vector3(0.85f, 1);

        }

        //Contentのサイズを調整する
        float contentSize = 51 * (_CellList.Count + 1);
        if (contentSize <= 350f)
        {
            contentSize = 350f;
        }
        _Content.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, contentSize);

        //AddButtonの位置を調整する
        pos = new Vector3(135, 30);
        pos.y += _CellListMove * (_CellList.Count + 1);
        _CellAddButton.transform.localPosition = pos;
    }

    #endregion

    #region Button Action Listen Control
    /// <summary>
    /// 本体のデフォルトモードの再生オプションを設定する
    /// </summary>
    /// <param name="state">デフォルトモードのモードindex</param>
    public void NormalActivationStateChange(int state)
    {
        if (_RadioCriticalSection == true)
            return;

        NormalActivationStateChange(state, false);
    }

    /// <summary>
    /// リモート制御アプリケーションでデフォルトモードの再生オプションを設定する
    /// </summary>
    /// <param name="state">デフォルトモードのモードindex</param>
    public void NormalActivationStateRemote(int state)
    {
        if ((int)_CurrentMoveState == state && _CurrentMoveState != PTN_STATE.PTN_RANDOM)
            return;

        _RadioCriticalSection = true;

        //まずトグル
        _NormalPanelToggles[state - 1].isOn = true;
        _NormalPanelToggleGroup.NotifyToggleOn(_NormalPanelToggles[state - 1]);

        _RadioCriticalSection = false;

        NormalActivationStateChange(state, true);
    }

    /// <summary>
    /// デフォルトモードの再生オプションを設定してくれる
    /// </summary>
    /// <param name="state">デフォルトモードのモードindex</param>
    /// <param name="isRemote">関数がリモートコントロールアプリケーション関連の関数で実行されたか</param>
    public void NormalActivationStateChange(int state, bool isRemote)
    {
        if (state == (int)_CurrentMoveState && _CurrentMoveState != PTN_STATE.PTN_RANDOM)
            return;

        ForcedOffCoroutine();

        int rndIndex = 0;
        {
            //Rnd配置後に++になってしまうと、既存の私の再生していたIndexを見つけなくなってしまうので
            //あらかじめIndexを探してくれるようである
            if (_RndToggleInput == true && (PTN_STATE)state == PTN_STATE.PTN_STOP)
            {
                if (_CurrentPlayMode <= 0)
                {
                    int temp = 0;
                    if (_Initialize == false)
                    {
                        if (_CurrentModeIndex == 0)
                        {
                            temp = _CellList.Count - 1;
                        }
                        else
                        {
                            temp = _CurrentModeIndex - 1;
                        }

                        rndIndex = _CellList[temp].Mode;
                    }
                    else
                        rndIndex = _CellList[0].Mode;
                }
                else
                {
                    rndIndex = _CurrentPlayMode;
                }

            }
        }
        bool comefromAuto3 = false;
        bool comefromAuto4 = false;
        bool comefromAuto5 = false;
        {
            //PTN_STOPの場合に適用してくれるもの
            //もしPTN_AUTO3から来た場合には、従来のIndexに10をよりヘジュオヤする
            if (_CurrentMoveState == PTN_STATE.PTN_AUTO_3)
                comefromAuto3 = true;
            else if (_CurrentMoveState == PTN_STATE.PTN_AUTO_4) //21から開始
                comefromAuto4 = true;
            else if (_CurrentMoveState == PTN_STATE.PTN_AUTO_5) //31から開始
                comefromAuto5 = true;
        }
        
        int tempIndex = 0;

        if (_CurrentMoveState != PTN_STATE.PTN_STOP
            && (PTN_STATE)state == PTN_STATE.PTN_STOP)
            tempIndex = _CurrentModeIndex;

        _CurrentMoveState = (PTN_STATE)state;

        ListSetting();

        if(tempIndex > 0)
            _CurrentModeIndex = tempIndex;

        if (_CurrentMoveState == PTN_STATE.PTN_STOP)
        {
            if (_RndToggleInput == true)
            {
                _CurrentModeIndex = rndIndex - 1;
                _RndToggleInput = false;
            }

            if (comefromAuto3 == true)
                _CurrentModeIndex += 10;

            if (comefromAuto4 == true)
                _CurrentModeIndex += 20;

            if (comefromAuto5 == true)
                _CurrentModeIndex += 30;

        }

        Set_ModeDisplayText(_CellList[_CurrentModeIndex].Mode);

        if (isRemote == false)
        {
            byte[] msg = new byte[1010];
            msg[0] = (byte)_CurrentMoveState;
            msg[1] = (byte)_CellList[_CurrentModeIndex].Mode;

            MsgHandler.instance.GenerateMsg(bluetoothMono.DATA_TYPE.DT_MODE_TOGGLE, bluetoothMono.DATA_ACTION.DA_NO_REQUIRE_ACTION, msg);
        }
        else
            SendPtrmsg(_CellList[_CurrentModeIndex].Mode);

        if (_PlayPatternNow == false)
        {

        }
        else
        {
            PlayCell();
        }
    }

    /// <summary>
    /// 電流パターンのIndexを前方に移動する
    /// </summary>
    public void ModeIndexForward()
    {
        if (_CurrentMoveState == PTN_STATE.PTN_MUSIC)
            return;


        if (_CurrentModeIndex + 1 <= _CellList.Count - 1)
            ++_CurrentModeIndex;
        else
            _CurrentModeIndex = 0;

        if (_PlayPatternNow == true && _CurrentMoveState != PTN_STATE.PTN_STOP)
        {
            ForcedOffCoroutine();
            PlayCell();
        }
        else
        {
            //重複したコードみたいに見える固定モードのとき、ここに入って来るので、重複ではない
            if (_PlayPatternNow == true)
            {
                ForcedOffCoroutine();
                PlayCell();
            }

            Set_ModeDisplayText(_CellList[_CurrentModeIndex].Mode);
        }

        if (bluetoothMono.instance.Connect == true)
        {
            byte[] msg = new byte[1010];
            msg[0] = (byte)_CellList[_CurrentModeIndex].Mode;

            MsgHandler.instance.GenerateMsg(bluetoothMono.DATA_TYPE.DT_PTR,
                bluetoothMono.DATA_ACTION.DA_NO_REQUIRE_ACTION, msg);
        }
    }

    /// <summary>
    /// 電流パターンのIndexを後ろに移動する
    /// </summary>
    public void ModeIndexBackward()
    {
        if (_CurrentMoveState == PTN_STATE.PTN_MUSIC)
            return;

        if (_CurrentModeIndex - 1 >= 0)
            --_CurrentModeIndex;
        else
            _CurrentModeIndex = _CellList.Count - 1;

        if (_PlayPatternNow == true && _CurrentMoveState != PTN_STATE.PTN_STOP)
        {
            ForcedOffCoroutine();
            PlayCell();
        }
        else
        {
            //重複したコードみたいに見える固定モードのとき、ここに入って来るので、重複ではない
            if (_PlayPatternNow == true)
            {
                ForcedOffCoroutine();
                PlayCell();
            }

            Set_ModeDisplayText(_CellList[_CurrentModeIndex].Mode);
        }

        if (bluetoothMono.instance.Connect == true)
        {
            byte[] msg = new byte[1010];
            msg[0] = (byte)_CellList[_CurrentModeIndex].Mode;

            MsgHandler.instance.GenerateMsg(bluetoothMono.DATA_TYPE.DT_PTR, bluetoothMono.DATA_ACTION.DA_NO_REQUIRE_ACTION, msg);
        }
    }

    /// <summary>
    /// 基本モードと詳細モードを切り替えてくれる関数
    /// </summary>
    /// <param name="viaRemote">リモコンアプリケーションから来たかどうか</param>
    public void NormalAdvancedToggle(bool viaRemote = false)
    {
        if (PulseMain.instance.Get_PlayStateIsMusic == true)
            return;

        if (_PlayPatternNow == true)
            PulseMain.instance.TurnOff();

        if (_IsNormalPtnOption == true)
        {
            _NormalPanel.SetActive(false);
            _AdvancedPanel.SetActive(true);

            _OptionToggleBtnLabel.text = FontSystem.instance.DataTransfer(FontSystem.FONT_ID.FID_BT_NOROPTION);
            _OptionToggleBtnImage.localScale = new Vector3(-1f, 1f, 1f);
            _IsNormalPtnOption = false;

            if(_CurrentMoveState != PTN_STATE.PTN_MUSIC &&
                _CurrentMoveState != PTN_STATE.PTN_CUSTOM)
                _PrevNormalState = _CurrentMoveState;

            if (_CurrentMoveState == PTN_STATE.PTN_STOP)
            {
                _FavLastIndex = _CurrentModeIndex;
            }
            else if (_CurrentMoveState == PTN_STATE.PTN_RANDOM)
                _FavLastIndex = _CellList[_CurrentModeIndex].Mode;
            else
                _FavLastIndex = _CurrentModeIndex;

            _CurrentMoveState = PTN_STATE.PTN_CUSTOM;

            //Clear後CustomLoadがあることを確認した後、ある場合引き渡すようしよう
            if (_CellList.Count > 0)
            {
                _CellList.Clear();
            }
            

            if (_AdvancedCellBackUp.Count > 0)
            {
                //バックアップしたものを読み込む
                LoadBackUp();
                AdvancedListLoad();
                _AdvancedCellBackUp.Clear();        //バグの原因になることもあり、何があるかわからないので、一度呼んで来たバックアップセルは飛ばしてくれる
            }
            else
            {
                //1つ新しく作成入れる
                AddCell();

                //1番目のセルのモードで初期化させてくれる
                _CurrentModeIndex = 0;
                Set_ModeDisplayText(_CellList[_CurrentModeIndex].Mode);
            }
        }
        else
        {
            _NormalPanel.SetActive(true);
            _AdvancedPanel.SetActive(false);

            _OptionToggleBtnLabel.text = FontSystem.instance.DataTransfer(FontSystem.FONT_ID.FID_BT_ADVOPTION);
            _OptionToggleBtnImage.localScale = new Vector3(1f, 1f, 1f);
            _IsNormalPtnOption = true;

            _CurrentMoveState = _PrevNormalState;

            for (int i = 0; i < _AdvancedOpCellObjects.Count; ++i)
            {
                Destroy(_AdvancedOpCellObjects[i]);
            }

            _AdvancedOpCellObjects.Clear();
            _Content.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 350f);
            _CellAddButton.transform.localPosition = new Vector3(135, -21);

            if (_CellList.Count > 0)
            {
                //詳細モードセルのバックアップ
                BackUpCellList();
            }

            ListSetting();

            _RadioCriticalSection = true;
            _NormalPanelToggles[(int)_CurrentMoveState - 1].isOn = true;
            _NormalPanelToggleGroup.NotifyToggleOn(_NormalPanelToggles[(int)_CurrentMoveState - 1]);
            _RadioCriticalSection = false;
        }

        Set_ModeDisplayText(_CellList[_CurrentModeIndex].Mode);

        if (bluetoothMono.instance.Connect == true)
        {
            byte[] msg = new byte[1010];
            msg[0] = (byte)(_IsNormalPtnOption == true ? 1 : 2);
            msg[1] = (byte)((int)_CurrentMoveState);
            msg[2] = (byte)_CellList[_CurrentModeIndex].Mode;

            MsgHandler.instance.GenerateMsg(bluetoothMono.DATA_TYPE.DT_NA_MODE, bluetoothMono.DATA_ACTION.DA_NO_REQUIRE_ACTION, msg);
        }

    }


    /// <summary>
    /// ボタン以外の方法でメニュー変更にアクセスする場合（デフォルト/詳細モード）
    /// </summary>
    public void NormalMode(bool fromFav = false)
    {
        // トグルボタンの設定値を変更する。
        NormalMusicTogglesChange(1);

        // メニューを変更する実際の関数を呼び出します。
        NormalMode_realFunc(fromFav);
    }

    /// <summary>
    /// ボタンを使用して、メニュー変更にアクセスする場合（デフォルト/詳細モード）
    /// </summary>
    public void OnChange_NormalModeBtn()
    {
        if (_NormalMusicToggles[1].isOn == true
            && _StopChangeModeAtBtn == false)
        {
            NormalMode_realFunc(false);
        }
    }

    /// <summary>
    /// 実際にモードの変更のためのプロセスが進行される関数（to デフォルト/詳細モード）
    /// </summary>
    /// <param name="fromFav"></param>
    private void NormalMode_realFunc(bool fromFav)
    {
        if (PulseMain.instance.Get_PlayStateIsMusic == false)
            return;

        _ModePanel.SetActive(true);
        _MusicPanel.SetActive(false);

        PulseMain.instance.MusicTurnChange(false);

        if (fromFav == false)
        {
            if (_PrevCustomModeOn == false)
                _CurrentMoveState = _PrevNormalState;
            else
            {
                _CurrentMoveState = PTN_STATE.PTN_CUSTOM;
                _PrevCustomModeOn = false;
            }
        }

        if (_CurrentMoveState == PTN_STATE.PTN_CUSTOM)
        {
            if (_IsNormalPtnOption == true)
            {
                NormalAdvancedToggle();
            }
            if (fromFav == false)
            {
                if (_AdvancedCellBackUp.Count >= 1)
                {
                    LoadBackUp();
                    AdvancedListLoad();
                }
                else
                {
                    AddCell();
                }
            }
        }
        else
        {
            if (_IsNormalPtnOption == false)
            {
                if(_CurrentMoveState != PTN_STATE.PTN_MUSIC
                    && _CurrentMoveState != PTN_STATE.PTN_CUSTOM)
                    _PrevNormalState = _CurrentMoveState;
                NormalAdvancedToggle();
            }
            ListSetting();
            if (_CurrentMoveState <= PTN_STATE.PTN_STOP && _CurrentMoveState >= PTN_STATE.PTN_AUTO_1)
            {
                _NormalPanelToggles[(int)_CurrentMoveState - 1].isOn = true;
                _NormalPanelToggleGroup.NotifyToggleOn(_NormalPanelToggles[(int)_CurrentMoveState - 1]);
            }
        }

        if (_CellList.Count > 0)
            Set_ModeDisplayText(_CellList[_CurrentModeIndex].Mode);
        else
            Set_ModeDisplayText(1);

        if (bluetoothMono.instance.Connect == true)
        {
            //本来なら動作中に、別々に吹き飛ばしてくれるのが原則だが、
            //どうせ飛んで行くことがメッセージであるため、のように入れて送ってくれる
            byte[] msg = new byte[1010];
            if (_CurrentMoveState != PTN_STATE.PTN_MUSIC)
            {
                msg[0] = (byte)(_IsNormalPtnOption == true ? 1 : 2);
                msg[1] = (byte)(_IsNormalPtnOption == true ? _CurrentMoveState : 0);
                if (_CellList.Count > 0)
                {
                    Debug.LogError(_CurrentModeIndex);
                    msg[2] = (byte)_CellList[_CurrentModeIndex].Mode;
                }
                else
                    msg[2] = 1;     //これFavから飛んでくる場合には、時々適用だめ飛ん行く場合がありますので、このようにしてくれる
            }
            else
            {
                msg[0] = 3;
                msg[1] = 0;
                msg[2] = 0;
            }

            MsgHandler.instance.GenerateMsg(bluetoothMono.DATA_TYPE.DT_NM_MODE,
                bluetoothMono.DATA_ACTION.DA_NO_REQUIRE_ACTION, msg);
        }
    }

    /// <summary>
    /// ボタン以外の方法でメニュー変更にアクセスする場合（音楽モード）
    /// </summary>
    public void MusicMode(bool viaRemote = false)
    {
        // トグルボタンの設定値を変更する。
        NormalMusicTogglesChange(0);

        // メニューを変更する実際の関数を呼び出します。
        MusicMode_realFunc(viaRemote);
    }

    /// <summary>
    /// ボタンを使用して、メニュー変更にアクセスする場合（音楽モード）
    /// </summary>
    public void OnChange_MusicModeBtn()
    {
        if (_NormalMusicToggles[0].isOn == true
            && _StopChangeModeAtBtn == false)
        {
            MusicMode_realFunc(false);
        }
    }

    /// <summary>
    /// 実際にモードの変更のためのプロセスが進行される関数（to 音楽モード)
    /// </summary>
    /// <param name="viaRemote"></param>
    private void MusicMode_realFunc(bool viaRemote)
    {
        if (PulseMain.instance.Get_PlayStateIsMusic == true)
            return;

        _ModePanel.SetActive(false);
        _MusicPanel.SetActive(true);

        PulseMain.instance.MusicTurnChange(true);

        if (_CurrentMoveState == PTN_STATE.PTN_CUSTOM)
        {
            BackUpCellList();
            for (int i = 0; i < _AdvancedOpCellObjects.Count; ++i)
            {
                Destroy(_AdvancedOpCellObjects[i]);
            }
            _PrevCustomModeOn = true;
        }
        else
        {
            if(_CurrentMoveState != PTN_STATE.PTN_MUSIC
                && _CurrentMoveState != PTN_STATE.PTN_CUSTOM)
                _PrevNormalState = _CurrentMoveState;

            if (_CurrentMoveState == PTN_STATE.PTN_RANDOM)
                _FavLastIndex = _CellList[_CurrentModeIndex].Mode;
            else
                _FavLastIndex = _CurrentModeIndex;
        }

        _CurrentMoveState = PTN_STATE.PTN_MUSIC;
        ForcedOffCoroutine();
        if (viaRemote == true)
        {
            bluetoothMono.instance.SendEcho();
        }
        else
        {
            if (bluetoothMono.instance.Connect == true)
            {
                byte[] msg = new byte[1010];
                msg[0] = 3;
                msg[1] = 0;

                MsgHandler.instance.GenerateMsg(bluetoothMono.DATA_TYPE.DT_NM_MODE,
                    bluetoothMono.DATA_ACTION.DA_NO_REQUIRE_ACTION, msg);
            }
        }
    }


    /// <summary>
    /// 音楽モードとデフォルト/詳細モードを切り替える
    /// </summary>
    /// <param name="State">モードindex</param>
    private void NormalMusicTogglesChange(int State)
    {
        // トグルボタンの表示を変える。
        // <注意！>どこで呼び出していたボタンに予約されたNormal/ MusicMode関数が発動される。

        // State
        // 1 = NormalMode
        // 0 = MusicMode

        if (_NormalMusicToggles[State].isOn == false)
        {
            // トグルが変更されるようにメニューを変更関数が動作しないように設定します。
            _StopChangeModeAtBtn = true;

            _NormalMusicToggles[State].isOn = true;
            _NormalMusicToggleGroup.NotifyToggleOn(_NormalMusicToggles[State]);


            // 以後トグルが変更されると、メニューの変更関数が動作するように設定します。
            _StopChangeModeAtBtn = false;
        }
    }

    /// <summary>
    /// モードindexのテキストを設定してくれる
    /// </summary>
    /// <param name="Value">モードnumber</param>
    private void Set_ModeDisplayText(int Value)
    {
        if (Value < 100)
            _ModeDisplayText.GetComponent<Text>().text = string.Format("{0:d2}", Value);
        else
            _ModeDisplayText.GetComponent<Text>().text = string.Format("{0:d3}", Value);
    }

    #endregion

    #region Cell List Player
    /// <summary>
    /// 一番最初のリストをセッティング
    /// </summary>
    public void InitializeListSetting()
    {
        ListSetting();
        Set_ModeDisplayText(_CellList[_CurrentModeIndex].Mode);
        
        _Initialize = true;
    }

    /// <summary>
    /// 止まっていた電流パターンの再生を再開する
    /// </summary>
    public void ResumePatternActivation()
    {
        PlayCell();
    }

    /// <summary>
    /// 電流パターンをプレイ
    /// </summary>
    private IEnumerator ModePlay()
    {
        // Coroutine実行周期の設定
        YieldInstruction Delay = new WaitForSeconds(0.05f);

        // PersePlayActive進行中のみ動作（実際には必要ありませんが、安全装置用）
        while (PulseMain.instance.PersePlayActive)
        {
            // _CurrentModeIndexの状態を確認する
            {
                // _CurrentModeIndexに対してOverFlowを確認する。
                Check_CurrentModeIndex();

                //Text Change
                Set_ModeDisplayText(_CellList[_CurrentModeIndex].Mode);

                if (bluetoothMono.instance.Connect == true)
                {
                    SendPtrmsg(_CellList[_CurrentModeIndex].Mode);
                }
            }


            // 今回Loopでプレイするモード
            var cell = _CellList[_CurrentModeIndex];

            // 完了までの時間を定義
            float EndTime = Time.time + cell.Time * 60f;

            //モード開始信号伝送
            PulseMain.instance.PatternLvSelect(cell.Mode);
            _CurrentPlayMode = cell.Mode;

            // タイマー稼動開始
            // PersePlayActive進行中のみ動作（実際には必要ありませんが、安全装置用）
            while (PulseMain.instance.PersePlayActive)
            {
                // 時間が終了すると、次のモードの設定+抜け出す
                if (EndTime <= Time.time)
                {
                    // 次のモードを呼び出すことができようにする。
                    if (_CurrentMoveState != PTN_STATE.PTN_STOP)
                    {
                        // _CurrentModeIndex 変更
                        ++_CurrentModeIndex;
                    }

                    break;
                }

                yield return Delay;
            }
        }

        // バグを備えた初期化
        _PlayCo = null;

    }

    /// <summary>
    /// _CurrentModeIndexに対してOverFlowを確認する。
    /// </summary>
    private void Check_CurrentModeIndex()
    {
        if (_CurrentModeIndex >= _CellList.Count)
        {
            //If current state is Random mode, reset list
            if (_CurrentMoveState == PTN_STATE.PTN_RANDOM)
                RandomModeSet();

            _CurrentModeIndex = 0;
        }
    }

    /// <summary>
    /// 再生のためのプロセスを進める
    /// </summary>
    private void PlayCell()
    {
        // _CurrentModeIndexに対してOverFlowを確認する。
        Check_CurrentModeIndex();

        //一種の安全装置。現時点でいくつかの不必要な存在。
        if (_PlayPatternNow == false)
            return;

        // 現在の高度なオプションの場合、オプションの操作が不可能に設定
        if (_CurrentMoveState == PTN_STATE.PTN_CUSTOM)
            _AdvancedClickBlock.SetActive(true);


        // 前鼻ルーチンが存在する場合は、停止
        if (_PlayCo != null)
            StopCoroutine(_PlayCo);

        //Play
        _PlayCo = StartCoroutine(ModePlay());
    }

    /// <summary>
    /// 強制的に再生を停止する
    /// </summary>
    public void ForcedOffCoroutine()
    {
        if (_PlayCo != null)
        {
            StopCoroutine(_PlayCo);
            _PlayCo = null;
        }

        _AdvancedClickBlock.SetActive(false);
        _CurrentPlayMode = -1;
    }
    #endregion

    #region Data transfer
    /// <summary>
    /// 現在再生されているパターンのリストの情報を返し
    /// </summary>
    /// <returns>パターンリスト</returns>
    public List<PTN_CELL> CellListReturn()
    {
        return _CellList;
    }

    /// <summary>
    /// 詳細モードのパターンリストをお気に入りから受けてい
    /// </summary>
    /// <param name="cellList">保存されていたパターンのリスト</param>
    public void SettingCustomCellListFromFav(List<PTN_CELL> cellList)
    {
        if (_PlayCo != null)
            ForcedOffCoroutine();

        //一度詳細設定に戻しておいてみましょう
        if (PulseMain.instance.Get_PlayStateIsMusic == true)
        {
            //音楽であれば、一度音楽を取り出してくれる
            NormalMode(true);
        }

        //詳細モードではない場合、詳細モードで合わせてくれる
        if (_CurrentMoveState != PTN_STATE.PTN_CUSTOM)
        {
            NormalAdvancedToggle();
        }

        //詳細モードで合わせてくれたから、中身を着替えかかる
        _CellList.Clear();

        if(_AdvancedOpCellObjects.Count > 0)
        {
            for(int i = 0; i < _AdvancedOpCellObjects.Count; ++i)
            {
                Destroy(_AdvancedOpCellObjects[i]);
            }

            _AdvancedOpCellObjects.Clear();
        }

        PTN_CELL cell;
        for (int i = 0; i < cellList.Count; ++i)
        {
            cell = new PTN_CELL();

            cell.SetCell(cellList[i].Mode, cellList[i].Time);
            _CellList.Add(cell);
        }

        _CurrentModeIndex = 0;

        AdvancedListLoad();

        Set_ModeDisplayText(_CellList[0].Mode);
    }

    /// <summary>
    /// お気に入りからロードされる音楽モード
    /// </summary>
    /// <param name="mode">音楽モードのモードナンバー</param>
    public void SettingMusicModeFromFav(int mode)
    {
        if (_PlayCo != null)
            ForcedOffCoroutine();

        MusicMode();

        //なぜなのかは分からないのに、ここで何度も出ていく
        //修正完了
        MusicPanal.Instance.Set_MusicListBtn_ToggleGroup(mode, 0);
       
    }

    /// <summary>
    /// お気に入りからロードされるデフォルトモード
    /// </summary>
    /// <param name="state">モード</param>
    /// <param name="lastNumber">保存されていたパターンナンバー</param>
    public void SettingModeFromFav(PTN_STATE state, int lastNumber)
    {
        if (_PlayCo != null)
            ForcedOffCoroutine();

        if (PulseMain.instance.Get_PlayStateIsMusic == true)
        {
            //音楽モード時、デフォルトモードに戻す
            NormalMode(true);
        }

        //詳細モード時、デフォルトモードに戻す
        if (_CurrentMoveState == PTN_STATE.PTN_CUSTOM)
        {
            NormalAdvancedToggle();
        }

        //ランダムモードなら別にしてくれるとする
        if (state == PTN_STATE.PTN_RANDOM)
        {
            RandomModeSet(lastNumber);
        }
        else
        {
            NormalActivationStateChange((int)state);
            _CurrentModeIndex = lastNumber;
        }

        Set_ModeDisplayText(_CellList[_CurrentModeIndex].Mode);

        //トグル変更により値が変わらないようにCritical Sectionを指定する
        _RadioCriticalSection = true;

        _NormalPanelToggles[(int)state - 1].isOn = true;
        _NormalPanelToggleGroup.NotifyToggleOn(_NormalPanelToggles[(int)state - 1]);

        _NormalMusicToggles[1].isOn = true;
        _NormalMusicToggleGroup.NotifyToggleOn(_NormalMusicToggles[1]);

        _RadioCriticalSection = false;
    }

    /// <summary>
    /// お気に入りに設定が変わった後、進行中の設定を一時的に保管
    /// </summary>
    /// <param name="state">進行中のモード</param>
    /// <param name="pattern">進行中のパターンナンバー</param>
    public void PrevSettingsAfterFavAccept(int state, int pattern)
    {
        if (state == (int)PTN_STATE.PTN_CUSTOM)
        {
            _PrevCustomModeOn = true;
            _IsNormalPtnOption = false;
            _FavLastIndex = 0;
        }
        else
        {
            if((PTN_STATE)state != PTN_STATE.PTN_MUSIC
                || (PTN_STATE)state != PTN_STATE.PTN_CUSTOM)
                _PrevNormalState = (PTN_STATE)state;

            _IsNormalPtnOption = true;
            _FavLastIndex = pattern;
        }
    }
    #endregion

    #region Advacned Cell Mode BackUp & Load Function
    /// <summary>
    /// 詳細モードのパターン情報をバックアップ
    /// </summary>
    private void BackUpCellList()
    {
        PTN_CELL cell;
        _AdvancedCellBackUp.Clear();
        for (int i = 0; i < _CellList.Count; ++i)
        {
            cell = new PTN_CELL();
            cell.SetCell(_CellList[i].Mode, _CellList[i].Time);

            _AdvancedCellBackUp.Add(cell);
        }

        _CurrentModeIndex = 0;
    }

    /// <summary>
    /// バックアップしておいたパターン情報をロード
    /// </summary>
    private void LoadBackUp()
    {
        PTN_CELL cell;
        _CellList.Clear();
        for (int i = 0; i < _AdvancedCellBackUp.Count; ++i)
        {
            cell = new PTN_CELL();
            cell.SetCell(_AdvancedCellBackUp[i].Mode, _AdvancedCellBackUp[i].Time);

            _CellList.Add(cell);
        }
        _CurrentModeIndex = 0;
    }
    #endregion

    #region bluetooth Related Funcion
    /// <summary>
    /// パターンナンバーがリモート制御アプリケーションによって変更
    /// </summary>
    /// <param name="value">上/下</param>
    public void PatternFromRemote(bool value)
    {
        if (value == true)
            ModeIndexForward();
        else
            ModeIndexBackward();
         
        if (_PlayPatternNow != true)
            SendPtrmsg(_CellList[_CurrentModeIndex].Mode);
    }

    /// <summary>
    /// モードの変更が行われたことについてのリモート制御アプリケーションに向かって通知
    /// </summary>
    /// <param name="value"></param>
    public void SendPtrmsg(int value)
    {
        if (bluetoothMono.instance.Connect == false)
            return;

        byte[] msg = new byte[1010];
        msg[0] = (byte)value;

        MsgHandler.instance.GenerateMsg(bluetoothMono.DATA_TYPE.DT_PTR, bluetoothMono.DATA_ACTION.DA_NO_REQUIRE_ACTION, msg);
    }

    /// <summary>
    /// 本体の最初の情報をリモート制御アプリケーションに向かって送信するときに、電流パターン/再生モードの情報を収集してくれる関数
    /// </summary>
    /// <returns></returns>
    public byte[] FirstdataTransfer()
    {
        byte[] msg = new byte[3];

        // 0 - 現在のモード（デフォルト/詳細/音楽）
        // 1 - 現在のモード（ディテール）
        // 2 - 現在プレイ中のパターン

        if (PulseMain.instance.Get_PlayStateIsMusic == true)
        {
            msg[0] = 3;

            //音楽モードパターンを持って来る
            msg[1] = 0;
            msg[2] = 0;     //音楽モードでは、パターンが別にないので0を記入（後日生じた場合取得するように変更）
        }
        else
        {
            msg[0] = (byte)(_IsNormalPtnOption == true ? 1 : 2);

            if (_IsNormalPtnOption == true)
            {
                msg[1] = (byte)_CurrentMoveState;
            }
            else
                msg[1] = 0;

            msg[2] = (byte)_CellList[_CurrentModeIndex].Mode;
        }

        return msg;
}
    #endregion
}
