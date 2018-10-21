using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;


/// <summary>
/// アプリケーションの起動時に一番最初出る画面のクラス
/// Bluetoothのペアリングに関する機能を盛り込んでいる
/// </summary>
public class DeviceListScene : MonoBehaviour {

    public static DeviceListScene instance;
    UIActor _Actor;
    BthCommunicator _BTH;
    AARController _AAR;
    RemoteMain _Main;

    [SerializeField]
    Transform _Content;
    [SerializeField]
    GameObject _BtnPrefab;

    List<DeviceListButton> _BtnList;

    /// <summary>
    /// Bluetooth装置の情報を含んでいる構造体
    /// </summary>
    public struct DeviceListButton
    {
        string _Address;
        public string Address { get { return _Address; } }
        string _Name;
        public string Name { get { return _Name; } }
        public GameObject _Button;

        public void Init(string add, string name, GameObject but)
        {
            _Address = add;
            _Name = name;
            _Button = but;
        }

        public void DestroyBtn()
        {
            Destroy(_Button);
        }
    }

	void Awake () {
        instance = this;
	}

    public void ListInit(RemoteMain main, UIActor actor, BthCommunicator bth, AARController aar)
    {
        _BtnList = new List<DeviceListButton>();
        _Actor = actor;
        _BTH = bth;
        _AAR = aar;
        _Main = main;
    }

    public void DestroyList()
    {
        for(int i = 0; i < _BtnList.Count; ++i)
        {
            _BtnList[i].DestroyBtn();
        }

        _BtnList.Clear();
    }

    /// <summary>
    /// Bluetoothのデバイスをスキャンしたとき、Bluetoothデバイスを検出すると呼び出される関数
    /// </summary>
    /// <param name="str">Bluetoothの情報</param>
    public void FindDevice(string str)
    {
        //0は Device Name
        //1は Device Address
        string[] tokens = str.ToString().Split('|');

        //Device Nameがnull/ Nullである場合には、表示しないように措置してくれる
        if (tokens[0].Equals("null") || tokens[0].Equals("Null"))
        {
            Debug.LogError("Name null / Null");
            return;
        }

        //既に存在しているアドレスであるため、新たに作らない
        if (DuplicateAddress(tokens[1]) == true)
        {
            Debug.LogError("Duplicate message");
            return;
        }

        DeviceListButton dlb = new DeviceListButton();
        GameObject btn = Instantiate(_BtnPrefab) as GameObject;
        dlb.Init(tokens[1], tokens[0], btn);
        _BtnList.Add(dlb);

        //onClickリスナー設定します。Indexを設定しておいてコネクトをすぐに呼び出すことができるようにしてくれる
        btn.GetComponent<Button>().onClick.AddListener(() => _Actor.ConnectDevice(_BtnList.Count - 1));

        //ボタンのParent設定
        btn.transform.SetParent(_Content);

        //ボタンの位置調整
        Vector3 pos = new Vector3(241.5f, -34f);
        pos.y -= 55f * (_BtnList.Count - 1);

        ((RectTransform)btn.transform).localPosition = pos;
        btn.transform.localScale = new Vector3(1, 1);
        btn.transform.GetChild(0).GetComponent<Text>().text = tokens[0];
    }

    /// <summary>
    /// Paringされた機器の表示
    /// </summary>
    /// <param name="str">Bluetoothの情報</param>
    public void PairedDevice(string str)
    {
        //0は Device Name
        //1は Device Address
        //元Paringされた機器は、別に分類しなければならが、企画から別にしないことに伴い、未使用
    }

    /// <summary>
    /// Paring作業を開始する 
    /// </summary>
    /// <param name="index">クリックされたデバイスのリストIndex</param>
    public void ConnectDevice(int index)
    {
        _Main.Connect(_BtnList[index].Name, _BtnList[index].Address); 
    }

    /// <summary>
    /// 重複アドレスが含まれてきたのかチェックする関数
    /// </summary>
    /// <param name="address">Bluetoothデバイスのアドレス</param>
    /// <returns></returns>
    bool DuplicateAddress(string address)
    {
        for(int i = 0; i < _BtnList.Count; ++i)
        { 
            if (address.Equals(_BtnList[i].Address) == true)
                return true;
        } 

        return false;
    }

    /// <summary>
    /// 特定の名前を持つ機器にのみ接続できるように、デバイス名をろ過する関数（未完成） 
    /// </summary>
    /// <param name="name">Bluetoothデバイスの名前</param>
    /// <returns></returns>
    bool NamingFilter(string name)
    {
        string filterFrontString = "";   //外部ファイルから受けに来るようにする
        string filterBackString = "";   //外部ファイルから受けに来るようにする
        bool isEqual = true;

        //接頭辞を分析する
        if (filterFrontString.Length > 0)
        {
            string nameFirst = name.Substring(0, filterFrontString.Length - 1);
            if (nameFirst.Equals(filterFrontString) == false)
                isEqual = false;
        }

        //接頭辞が異常がない場合、接尾辞を分析する
        if (isEqual == true && filterBackString.Length > 0)
        {
            string nameEnd = name.Substring(name.Length - filterBackString.Length - 1);
            if (nameEnd.Equals(filterBackString) == false)
                isEqual = false;
        }
        
        return isEqual;
    }

    /// <summary>
    /// Paringが成功したので、メイン画面に進み行くのでListにあるボタンをすべて除去 
    /// </summary>
    public void ConnectSuccess()
    {
        DestroyList();
    }

    /// <summary>
    /// 何らかの理由でParing作業が中断された。状況に応じた初期化処理を実施する 
    /// </summary>
    public void Disconnect()
    {
        if(_BtnList.Count > 0)
            DestroyList();

        MusicPlayer.instance.ExitButtonPush();  //もし音楽ポップアップウィンドウが浮いている場合オフにしなければならない

        //QRコードスキャンウィンドウ浮いている場合とない場合の動作が異なる
        if (QRBthEvent.instance.IsActive == false)
            _AAR.Call("DeviceListCall");
        else
            QRBthEvent.instance.RestartQRReader();
    }

    /// <summary>
    /// Scan操作をもう一度実施する
    /// </summary>
    public void ReScanList()
    {
        if(_BtnList.Count > 0)
            DestroyList();

        _AAR.Call("DeviceListCall");
    }
}
