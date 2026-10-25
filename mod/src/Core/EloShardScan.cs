using System;
using System.Collections.Generic;
using Yx.Shared;
namespace YxPrediction
{
    /// <summary>Scan one JSON object in bounded chunks; deserialize only explicitly requested top-level UID rows.</summary>
    public sealed class EloShardScan
    {
        readonly string _text;
        readonly string[] _uids;
        readonly Dictionary<string,object> _rows=new Dictionary<string,object>();
        readonly char[] _stack=new char[64];
        static readonly char[] ValueMarks = new char[] { '"', '{', '}', '[', ']' };
        static readonly char[] StringMarks = new char[] { '"', (char)92 };
        int _at,_phase,_start,_depth;
        bool _string,_escape,_wanted,_keyEscaped;
        string _key;
        public bool Done { get { return _phase==7; } }
        public int Position { get { return _at; } }
        public Dictionary<string,object> Rows { get { return _rows; } }
        public EloShardScan(string text,string[] uids)
        {
            if(text==null||text.Length>2097152)throw new FormatException("Elo响应过大");
            _text=text;_uids=uids;
        }
        static bool Space(char c){return c==' '||c==(char)9||c==(char)10||c==(char)13;}
        public void Step(int budget)
        {
            if(budget<1)throw new ArgumentOutOfRangeException("budget");
            int end=Math.Min(_text.Length,_at+budget);
            while(_at<end&&!Done)
            {
                // BCL performs the long scans outside the ILRuntime character loop.
                // Keep the same budget, escape state and structural validation at boundaries.
                if (_phase == 4 && _start >= 0 && !_escape)
                {
                    int mark = _text.IndexOfAny(_string ? StringMarks : ValueMarks, _at, end - _at);
                    if (mark < 0) { _at = end; break; }
                    _at = mark;
                }
                char c=_text[_at++];
                if(_phase==0){if(Space(c))continue;if(c!='{')throw new FormatException("Elo分片不是对象");_phase=1;continue;}
                if(_phase==1||_phase==6)
                {
                    if(Space(c))continue;
                    if(c=='}'&&_phase==1){_phase=5;continue;}
                    if(c!='"')throw new FormatException("Elo分片键无效");
                    _start=_at-1;_escape=false;_keyEscaped=false;_phase=2;continue;
                }
                if(_phase==2)
                {
                    if(_at-_start>256)throw new FormatException("Elo分片键过长");
                    if(_escape){_escape=false;continue;}if(c==(char)92){_escape=true;_keyEscaped=true;continue;}
                    if(c=='"'){_key=_keyEscaped?Json.Parse(_text.Substring(_start,_at-_start)) as string:_text.Substring(_start+1,_at-_start-2);_wanted=false;for(int i=0;i<_uids.Length;i++)if(_uids[i]==_key)_wanted=true;_phase=3;}
                    continue;
                }
                if(_phase==3){if(Space(c))continue;if(c!=':')throw new FormatException("Elo分片缺少冒号");_phase=4;_depth=0;_start=-1;continue;}
                if(_phase==4)
                {
                    if(_start<0){if(Space(c))continue;if(c!='{')throw new FormatException("Elo玩家资料不是对象");_start=_at-1;_depth=1;_stack[0]='}';_string=false;_escape=false;continue;}
                    if(_wanted&&_at-_start>16384)throw new FormatException("Elo玩家资料过大");
                    if(_string){if(_escape)_escape=false;else if(c==(char)92)_escape=true;else if(c=='"')_string=false;continue;}
                    if(c=='"'){_string=true;continue;}
                    if(c=='{'||c=='['){if(_depth==64)throw new FormatException("Elo资料嵌套过深");_stack[_depth++]=c=='{'?'}':']';continue;}
                    if(c=='}'||c==']')
                    {
                        if(_depth<1||_stack[--_depth]!=c)throw new FormatException("Elo资料括号不匹配");
                        if(_depth==0)
                        {
                            _phase=8;
                            if(_wanted){if(_rows.ContainsKey(_key))throw new FormatException("Elo UID重复");_rows[_key]=Json.ParseObject(_text.Substring(_start,_at-_start));return;}
                        }
                    }
                    continue;
                }
                if(_phase==8){if(Space(c))continue;if(c==','){_phase=6;continue;}if(c=='}'){_phase=5;continue;}throw new FormatException("Elo分片分隔符无效");}
                if(_phase==5){if(!Space(c))throw new FormatException("Elo分片尾部无效");}
            }
            if (_phase == 4 && _wanted && _start >= 0 && _at - _start > 16384) throw new FormatException("Elo玩家资料过大");
            if(_at==_text.Length){if(_phase!=5)throw new FormatException("Elo分片被截断");_phase=7;}
        }
    }
}
