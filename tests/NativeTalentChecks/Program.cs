using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YxPrediction;
namespace UnityEngine
{
 public struct Vector2 { public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 zero{get{return new Vector2();}} }
 public struct Vector3 { public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 one{get{return new Vector3(1,1,1);}} }
 public struct Quaternion { public float x;public static Quaternion identity{get{return new Quaternion();}} }
 public class Transform
 {
  public Transform parent;public GameObject gameObject;
  public Vector3 localScale=Vector3.one;public Quaternion localRotation;
  public Vector3 position {get {var r=this as RectTransform;return r==null?new Vector3():new Vector3(r.anchoredPosition.x,r.anchoredPosition.y,0);}}
 }
 public class RectTransform:Transform { public Vector2 anchorMin,anchorMax,pivot,anchoredPosition,sizeDelta;public object Layout;public object GetComponent(Type type){return Layout;} }
 public class GameObject { public Transform transform;public bool activeInHierarchy=true; }
 public static class Time {public static float unscaledTime;}
}
namespace UnityEngine.UI {public class LayoutGroup {public bool enabled=true;}}
namespace Yx.ModSdk {public class ModContext {public TestLog Log=new TestLog();}public class TestLog{public List<string> Warnings=new List<string>();public void Warn(string s){Warnings.Add(s);}}}
public class BattleTalentIconItem
{
 private bool m_IsZoom=true;
 public Transform transform;
 public Vector3 baseScale=new Vector3(1,1,1),basePos=new Vector3(99,88,0);
 public bool Zoomed{get{return m_IsZoom;}}
 public void Zoom(bool value){m_IsZoom=value;}
}
public class NativePanel {public bool isShow=true;public GameObject gameObject=new GameObject();}
public class OpponentInfoPanel {private IList m_Talents;public NativePanel panel=new NativePanel();public OpponentInfoPanel(IList list){m_Talents=list;}}
public class BattlePanel {public OpponentInfoPanel Opponent;public int Lookups;public T FindILRSubPanel<T>(){Lookups++;return (T)(object)Opponent;}}
namespace YxPrediction
{
 public static class PredictionVisibility {public static bool Observer=true;public static bool CanShow(){return Observer;}}
 public static class GameBridge {public static bool Battle;public static BattlePanel Panel;public static bool IsPlayingBattle(){return Battle;}public static BattlePanel GetBattlePanel(){return Panel;}}
}
internal static class Program
{
 static int checks;
 static void Check(bool value){checks++;if(!value)throw new Exception("Native talent regression "+checks);}
 static List<BattleTalentIconItem> Make(RectTransform parent,int count)
 {
  var items=new List<BattleTalentIconItem>();
  for(int i=0;i<count;i++){
   var rect=new RectTransform{parent=parent,anchorMin=new Vector2(.2f,.3f),anchorMax=new Vector2(.4f,.5f),pivot=new Vector2(.1f,.9f),anchoredPosition=new Vector2(120+200*i,-100),sizeDelta=new Vector2(160,160),localScale=Vector3.one};
   rect.gameObject=new GameObject{transform=rect};items.Add(new BattleTalentIconItem{transform=rect});
  }
  return items;
 }
 static void Main()
 {
  var group=new LayoutGroup();var root=new RectTransform{anchoredPosition=new Vector2(56,176),sizeDelta=new Vector2(1000,200),Layout=group};var items=Make(root,6);
  var layout=new NativeTalentLayout();layout.Apply(items);
  Check(root.anchorMin.x==0&&root.anchorMin.y==0&&root.anchoredPosition.x==270&&root.anchoredPosition.y==182);
  Check(root.sizeDelta.x==528&&!group.enabled);
  float[] xs={0,-105,-190,-197,-148,-46},ys={-145.9f,-128.4f,-60.9f,40.6f,137.4f,189},scales={.6f,.65f,.7f,.75f,.8f,.8f};
  for(int i=0;i<6;i++){
   var rect=(RectTransform)items[i].transform;
   Check(rect.parent==root);Check(rect.anchoredPosition.x==xs[i]&&rect.anchoredPosition.y==ys[i]);
   Check(rect.localScale.x==scales[i]&&items[i].baseScale.x==scales[i]);Check(!items[i].Zoomed);Check(rect.sizeDelta.x==128);
   Check(rect.anchorMin.x==.5f&&rect.anchorMin.y==.5f);
  }
  items[2].Zoom(true);layout.Apply(items);Check(!items[2].Zoomed);layout.Apply(items);
  layout.Restore();Check(root.anchoredPosition.x==56&&root.anchoredPosition.y==176&&root.sizeDelta.x==1000&&group.enabled);
  for(int i=0;i<6;i++){
   var rect=(RectTransform)items[i].transform;Check(rect.parent==root&&rect.anchoredPosition.x==120+200*i&&rect.anchoredPosition.y==-100);
   Check(rect.localScale.x==1&&items[i].baseScale.x==1&&items[i].basePos.x==99&&items[i].Zoomed);
  }
  var otherRoot=new RectTransform();var other=Make(otherRoot,5);layout.Apply(items);layout.Apply(other);Check(root.anchoredPosition.x==56);layout.Restore();Check(otherRoot.anchoredPosition.x==0);
  // Locked/unavailable slots stay in their original position in the sequence.
  var missing=new ArrayList(items);missing[1]=null;layout.Apply(missing);Check(((RectTransform)items[2].transform).anchoredPosition.x==-190);layout.Restore();
  // Own/native display is not transformed by the observer adapter when no opponent is shown.
  var ctx=new Yx.ModSdk.ModContext();GameBridge.Panel=new BattlePanel();var orbit=new TalentOrbit(ctx);Time.unscaledTime=1;orbit.Update();Check(root.anchoredPosition.x==56&&ctx.Log.Warnings.Count==0);
  GameBridge.Panel.Opponent=new OpponentInfoPanel(items);Time.unscaledTime=2;orbit.Update();Check(root.anchoredPosition.x==270);
  GameBridge.Battle=true;Time.unscaledTime=3;orbit.Update();Check(root.anchoredPosition.x==56);GameBridge.Battle=false;
  PredictionVisibility.Observer=false;Time.unscaledTime=4;orbit.Update();Check(root.anchoredPosition.x==56);PredictionVisibility.Observer=true;
  Time.unscaledTime=5;orbit.Update();orbit.Destroy();Check(root.anchoredPosition.x==56&&items[0].Zoomed);
  Console.WriteLine("Native talent layout checks passed: "+checks+" (production code, offline doubles; no game rendering)");
 }
}
