using System;
using TMPro;
using UnityEngine;

namespace SwanJourney
{
    // Keep interaction phases for diagnostics; both console caption strips are hidden.
    [DefaultExecutionOrder(3920)]
    public sealed class SwanConsoleTeaching : MonoBehaviour
    {
        public SteamJourney journey;
        public SwanJourneyInteraction input;
        public StudioSpatialConsoleV18 console;
        public StudioSpatialRefineV19 driving;
        public TMP_Text departureLabel;
        public string Phase {get;private set;}
        public string Heading {get;private set;}
        public string Instruction {get;private set;}
        float nextRefresh,seatedUntil;
        string seatedTitle;
        static readonly Color Ready=new Color(.70f,.94f,.84f);

        void OnEnable(){if(journey)journey.PartSeated+=Seated;nextRefresh=0;}
        void OnDisable(){if(journey)journey.PartSeated-=Seated;}
        void Seated(JourneyModule part){seatedTitle=DisplayTitle(part.title);seatedUntil=Time.unscaledTime+2.4f;nextRefresh=0;}
        void LateUpdate()
        {
            if(Time.unscaledTime<nextRefresh)return;
            nextRefresh=Time.unscaledTime+.10f;
            Refresh();
        }
        public void Refresh()
        {
            if(!journey || !console)return;
            Resolve();
            if(driving && driving.driveMessage)
            {
                Set(driving.driveMessage,"");
                if(driving.driveMessage.gameObject.activeSelf)driving.driveMessage.gameObject.SetActive(false);
            }
            if(journey.inSandbox)
            {
                return;
            }
            if(departureLabel)
            {
                Set(departureLabel,"出发");
                departureLabel.color=journey.Complete?Ready:new Color(.65f,.63f,.56f);
            }
        }
        void Resolve()
        {
            if(journey.SwanSceneLoading || journey.SwanTransitionActive)
            {Show("transition","正在准备场景","稍候，场景展开后即可操作");return;}
            if(journey.inSandbox)
            {
                if(driving && driving.ReturnOpen)
                    Show("return","返回工坊","按下盖板内的按钮，返回组装台");
                else if(journey.brake>.5f)
                    Show("braking","制动中","先松开制动，再推汽门；停稳后可换向");
                else if(journey.throttle<.05f && journey.speed<.03f)
                    Show("ready-to-drive","准备行驶","向前推汽门启动列车 · 制动手柄用于停车");
                else Show("driving","山谷铁路","汽门调速度 · 制动可停车 · 停稳后换向");
                return;
            }
            if(input && input.Exploding)
            {Show("disassembly","正在拆开机车","零件停稳后，可以重新拿取组装");return;}
            if(journey.AutoAssembling)
            {Show("automatic","自动组装中","零件正在依次归位，请稍候");return;}
            if(journey.CompletionActive)
            {Show("celebrating","机车装配完成","完成展示后，按「出发」进入山谷");return;}
            if(journey.Complete)
            {Show("complete","机车装配完成","按「出发」进入山谷 · 也可按「验证」观察原理");return;}
            var part=journey.SwanHeldModule;
            if(input && input.QuickReturning)
            {Show("seating","零件正在归位","稍候，装好后会显示下一个组件");return;}
            if(part!=null)
            {
                if(journey.SnapReady)Show("aligned",DisplayTitle(part.title)+" · 已对齐","松开捏合，将零件装入接口");
                else if(!journey.SwanCanInstall(part))Show("out-of-order",DisplayTitle(part.title),"先安装当前步骤的组件，再装这个零件");
                else Show("holding",DisplayTitle(part.title),"捏住零件移向蓝色轮廓 · 对齐后松手");
                return;
            }
            var notice=journey.SpatialNotice;
            // Retain error/action notices from the original state machine.
            if(!string.IsNullOrEmpty(notice) && !notice.Contains("已装好") && !notice.Contains("从展示台拿起")
                && !notice.Contains("验证已开启") && !notice.Contains("验证已关闭"))
            {Show("notice","操作提示",notice);return;}
            if(Time.unscaledTime<seatedUntil && journey.modules[0].installed)
            {Show("seated",seatedTitle+" · 已装好",journey.running?"继续安装下一件，观察动力传递的变化":"可按「验证」观察原理，也可继续安装下一件");return;}
            int index=Array.IndexOf(journey.modules,journey.Current);
            if(index<0)index=journey.modules.Length+Array.IndexOf(journey.finishing,journey.Current);
            string title=(index+1).ToString("00")+" / "+(journey.modules.Length+journey.finishing.Length).ToString("00")+"  "+DisplayTitle(journey.Current.title);
            if(journey.running)
                Show("observing",title,journey.paused?"演示已暂停 · 按「暂停 / 继续」恢复":"正在演示已装部件 · 转动「观察」查看内部");
            else if(journey.Current.parked)
                Show("parked",title,"拿回零件对齐安装 · 也可连续捏合两次归位");
            else Show("pick-up",title,"捏住台上零件移向蓝色轮廓 · 对齐后松手");
        }
        void Show(string phase,string heading,string instruction){Phase=phase;Heading=heading;Instruction=instruction;}
        public static string DisplayTitle(string value)
        {
            if(string.IsNullOrEmpty(value))return "";
            int digits=0;while(digits<value.Length && char.IsDigit(value[digits]))digits++;
            int start=digits;while(start<value.Length && (char.IsWhiteSpace(value[start]) || value[start]=='.' || value[start]=='·'))start++;
            return digits>0 && start>digits && start<value.Length?value.Substring(start):value;
        }
        static void Set(TMP_Text text,string value){if(text && text.text!=value)text.text=value;}
    }
}
