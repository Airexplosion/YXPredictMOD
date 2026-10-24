using System;
using UnityEngine;
using YxPrediction;
internal static class Program
{
    static int checks;
    static void Check(bool condition) { checks++; if (!condition) throw new Exception("Hover regression " + checks); }
    static void Main()
    {
        foreach (bool inverse in new[] {false,true})
        foreach (bool focus in new[] {false,true})
        foreach (bool hover in new[] {false,true})
        {
            Check(HorsePlateLayout.ShouldShow(true,true,inverse,"p",focus?"p":"other",hover)==(inverse?(focus||hover):!(focus||hover)));
            Check(!HorsePlateLayout.ShouldShow(false,true,inverse,"p","other",false));
            Check(!HorsePlateLayout.ShouldShow(true,false,inverse,"p","other",false));
            Check(!HorsePlateLayout.ShouldShow(true,true,inverse,null,"other",false));
        }
        foreach(float scale in new[]{0.65f,1f,1.5f})
        {
            var parent=new RectTransform{sizeDelta=new Vector2(900,700),localScale=new Vector3(scale,scale,1)};
            var avatar=new RectTransform{parent=parent,sizeDelta=new Vector2(80,80),pivot=new Vector2(0.5f,0.5f)};
            var name=new RectTransform{parent=avatar,anchorMin=new Vector2(0.5f,0.5f),anchorMax=new Vector2(0.5f,0.5f),sizeDelta=new Vector2(150,28),pivot=new Vector2(0.5f,0.5f),anchoredPosition=new Vector2(0,-65)};
            var caption=new RectTransform{parent=avatar,anchorMin=new Vector2(0.5f,0.5f),anchorMax=new Vector2(0.5f,0.5f),sizeDelta=new Vector2(160,40),pivot=new Vector2(0.5f,0.5f),anchoredPosition=new Vector2(0,-105)};
            Rect area=HorsePlateLayout.HoverBounds(avatar,name,caption,caption.rect);
            Check(HorsePlateLayout.ContainsPoint(area,new Vector2(39,39)));
            Check(HorsePlateLayout.ContainsPoint(area,new Vector2(0,-50)));
            Check(HorsePlateLayout.ContainsPoint(area,new Vector2(70,-65)));
            Check(HorsePlateLayout.ContainsPoint(area,new Vector2(70,-105)));
            Check(!HorsePlateLayout.ContainsPoint(area,new Vector2(300,300)));
            Check(HorsePlateLayout.HoverDistance(avatar,area,new Vector2(0,-65))<HorsePlateLayout.HoverDistance(avatar,area,new Vector2(60,-65)));
            avatar.sizeDelta=Vector2.zero;
            Check(!HorsePlateLayout.ContainsPoint(HorsePlateLayout.HoverBounds(avatar,name,caption,caption.rect),Vector2.zero));
        }
        Console.WriteLine("Horse hover checks passed: "+checks+" (layout models, not game rendering)");
    }
}
