using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

/*
     
 */
/// <summary>
/// UIに使用されるボタンのActionをすべて集めたクラス。
/// この場所を経て、実際に動作されている関数で経与える作ってくれる。
/// ほとんどinspectorにかかっていて、いくつかの因子が可変である関数は、スクリプト内で直接宣言
/// </summary>
public class UIActor : MonoBehaviour {

    public static UIActor instance;
    RemoteMain _Main;
    DeviceListScene _List;
    MainBGScene _UI;
    Fav _Fav;

    [SerializeField]
    GameObject _DojaButtons;

	void Awake()
    {
        instance = this;
    }

    public void UIInit(RemoteMain main, DeviceListScene list, MainBGScene mainBg, Fav fav)
    {
        _Main = main;
        _List = list;
        _UI = mainBg;
        _Fav = fav;
    }

    /// <summary>
    /// 各パッドの電流強度上昇/下降のボタンイベントを追加
    /// </summary>
    public void DojaButtonsAddEvent()
    {
        Transform trans = _DojaButtons.transform;
        
        for (int i =0; i < trans.childCount; ++i)
        {
            BtnUpdownAddListner(trans.GetChild(i), i);
        }
    }

    /// <summary>
    /// 各パッドの電流強度上昇/下降のボタンイベントを追加
    /// </summary>
    /// <param name="parent">指定パッド</param>
    /// <param name="index">パッドの指定ナンバー</param>
    private void BtnUpdownAddListner(Transform parent, int index)
    {
        parent.Find("Up").GetComponent<Button>().onClick.AddListener(() => CountUp(index));
        parent.Find("Down").GetComponent<Button>().onClick.AddListener(() => CountDown(index));
    }

    /// <summary>
    /// 電流の強さの上昇
    /// </summary>
    /// <param name="type">パッドの指定ナンバー</param>
    public void CountUp(int type)
    {
        _UI.DojaLv(type, true);
    }

    /// <summary>
    /// 電流の強さ下降
    /// </summary>
    /// <param name="type">パッドの指定ナンバー</param>
    public void CountDown(int type)
    {
        _UI.DojaLv(type, false);
    }

    /// <summary>
    /// 本体の電流再生をOn / Off
    /// </summary>
    public void PowerOnOff()
    {
        _UI.PowerOnOff();
    }

    /// <summary>
    /// 電流パターンのナンバーを前進
    /// </summary>
    public void ModeForward()
    {
        _UI.ModeIndexCount(true);
    }

    /// <summary>
    /// 電流パターンのナンバーを後進
    /// </summary>
    public void ModeBackWard()
    {
        _UI.ModeIndexCount(false);
    }

    /// <summary>
    /// デフォルトモードのトグルボタンを押す
    /// </summary>
    /// <param name="index">ラジオボタンindex</param>
    public void NormalPatternModeSelect(int index)
    {
        _UI.NormalRadioSetMessageSend(index); 
    }

    /// <summary>
    /// タイマー再生時間の増加
    /// </summary>
    /// <param name="min">再生時間単位（分）</param>
    public void TimerUp(int min)
    {
        _UI.TimerSet(min, true);
    }

    /// <summary>
    /// タイマー再生時間の短縮
    /// </summary>
    /// <param name="min">再生時間単位（分）</param>
    public void TimerDown(int min)
    {
        _UI.TimerSet(min, false);
    }

    /// <summary>
    /// 温度段階の増加
    /// </summary>
    public void TempUp()
    { 
        _UI.TemperatureLv(true);
    }

    /// <summary>
    /// 温度段階の減少
    /// </summary>
    public void TempDown()
    {
        _UI.TemperatureLv(false);
    }

    /// <summary>
    /// 音楽モードのOn / Off
    /// </summary>
    public void MusicModeTurnOnOff()
    {
        _UI.MusicModeOn();
    }

    /// <summary>
    /// 次の音楽ファイルを再生する
    /// </summary>
    public void MusicFileForward()
    {
        _UI.MusicFileForwardbackward(true);
    }

    /// <summary>
    /// 以前の音楽ファイルの再生
    /// </summary>
    public void MusicFileBackward()
    {
        _UI.MusicFileForwardbackward(false);
    }

    /// <summary>
    /// デフォルトモード/詳細モードトグル
    /// </summary>
    public void NormalAdvancedToggle()
    {
        _UI.NormalAdvancedToggle();
    }

    /// <summary>
    /// デフォルトモードのトグルボタンを押す
    /// </summary>
    /// <param name="index">ラジオボタンindex</param>
    public void ModeRadioToggle(int index)
    {
        _UI.NormalRadioSetMessageSend(index);
    }

    /// <summary>
    /// お気に入りセーブポップアップ実行
    /// </summary>
    public void FavSavePopUpOn()
    {
        _Fav.InitSave();
    }

    /// <summary>
    /// お気に入りセーブポップアップ削除
    /// </summary>
    public void FavSavePopUpOff()
    {
        _Fav.ExitFavSavePopup();
    }

    /// <summary>
    /// お気に入りセーブ実行
    /// </summary>
    /// <param name="index">リストIndex</param>
    public void FavSave(int index)
    {
        _Fav.SendSaveMessage(index);
    }

    /// <summary>
    /// お気に入りロード実行
    /// </summary>
    /// <param name="index">リストIndex</param>
    public void FavSelect(int index)
    {
        Debug.LogError(index);
        _Fav.SendLoadMsg(index);
    }

    /// <summary>
    /// お気に入りロードポップアップ実行
    /// </summary>
    public void FavLoadPopUpOn()
    {
        _Fav.InitLoad();
    }

    /// <summary>
    /// お気に入りロードポップアップ削除
    /// </summary>
    public void FavLoadPopUpOff()
    {
        _Fav.ExitFavLoadPopup();
    }

    /// <summary>
    /// 音の音量を上げる
    /// </summary>
    public void VolumeUp()
    {
        _UI.VolumeControl(true);
    }

    /// <summary>
    /// 音の音量を下げる
    /// </summary>
    public void VolumeDown()
    {
        _UI.VolumeControl(false);
    }


    //重複している関数とすることができますが、UIのActionを集めたところで通するようにしなければなら検索が楽なので
    //ここにまず関数を作っておく
    /// <summary>
    /// Bluetooth接続を試みる
    /// </summary>
    /// <param name="i">装置リストindex</param>
    public void ConnectDevice(int i)
    {
        _List.ConnectDevice(i);
    }

    /// <summary>
    /// Bluetooth接続を壊す
    /// </summary>
    public void Disconnection()
    {
        _Main.Disconnection();
    }

    /// <summary>
    /// Bluetooth装置を探す
    /// </summary>
    public void ScanDevice()
    {
        _List.ReScanList();
    }

    /// <summary>
    /// 最後に接続したBluetoothのデバイスとの接続を試みる
    /// </summary>
    public void ConnectLastDevice()
    {
        _Main.ConnectLastDevice();
    }

    /// <summary>
    /// 現在自分の携帯電話のBluetoothの状態をスキャン可能に変更する
    /// </summary>
    public void DiscoverableModeOn()
    {
        AARController.Instance.Call("ensureDiscoverable");
    }


    //Stream
    /// <summary>
    /// 該当する音楽ファイルを転送する
    /// </summary>
    /// <param name="i">音楽リストindex</param>
    public void FileStream(int i)
    {
        //_Stream.ReadyToStream(i);
    }

    /// <summary>
    /// 音楽リストポップアップを実行する
    /// </summary>
    public void ListInitButton()
    {
        MusicPlayer.instance.PopupButtonPush();
    }

    /// <summary>
    /// 音楽リストポップアップを削除する
    /// </summary>
    public void ListInitPopupOff()
    {
        MusicPlayer.instance.ExitButtonPush();
    }


    //QRCode
    /// <summary>
    /// QRコードポップアップ実行
    /// </summary>
    public void QRPopupOn()
    {
        QRBthEvent.instance.PopupWindowOn();
    }

    /// <summary>
    /// QRコードポップアップ削除
    /// </summary>
    public void QRPopupOff()
    {
        QRBthEvent.instance.PopupWindowOff();
    }
}
