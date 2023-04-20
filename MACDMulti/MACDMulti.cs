using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.None)]
    public class MACDMulti : Robot
    {
        private int cTimeFrame;

        [Parameter(DefaultValue = "EURUSD,EURJPY")]
        public string CustomSymbols { get; set; }
        [Parameter(DefaultValue = "Run")]
        public string Label { get; set; }

        [Parameter(DefaultValue = 0.3)]
        public double Volume { get; set; }

        [Parameter(DefaultValue = 200)] public double MaxLoss { get; set; }
        [Parameter] public bool Flip { get; set; }
        [Parameter(MinValue = 2, MaxValue = 60)] public int Long { get; set; }

        [Parameter(MinValue = 2, MaxValue = 60)] public int Signal { get; set; }

        [Parameter(MinValue = 2, MaxValue = 60)] public int Short { get; set; }

        public List<string> AllSymbols { get; private set; }
        public List<Bars> AllBars { get; private set; }
        public List<MacdCrossOver> macdCrossOvers { get; private set; }

        protected override void OnStart()
        {
            if(Short >= Long)
            {
                Stop();
            }
            {
                if (Bars.TimeFrame.ShortName.StartsWith("m"))
                {
                    cTimeFrame = int.Parse(string.Join(' ', Bars.TimeFrame.ShortName.Skip(1)).Replace(" ", ""));
                }
                else if (Bars.TimeFrame.ShortName.StartsWith("h"))
                {
                    cTimeFrame = int.Parse(string.Join(' ', Bars.TimeFrame.ShortName.Skip(1)).Replace(" ", "")) * 60;
                }
                else
                {
                    Stop();
                }
            }

            AllSymbols = CustomSymbols.Split(',').ToList();
            AllBars = CustomSymbols.Split(',').Select(x => MarketData.GetBars(TimeFrame, x)).ToList();
            macdCrossOvers = CustomSymbols.Split(',').Select(x => Indicators.MacdCrossOver(MarketData.GetBars(TimeFrame, x).ClosePrices, Long, Short, Signal)).ToList();
            CustomSymbols.Split(',').Select(x => MaxLoss / Symbols.GetSymbolInfo(x).PipSize);
            // var series = MarketData.GetBars(TimeFrame, "EURUSD");
        }

        protected override void OnTick()
        {
            foreach (var symbol in AllSymbols)
            {
                foreach (var position in Positions.FindAll(Label + " " + symbol, symbol))
                {
                    if(position.NetProfit <= -MaxLoss)
                    {
                        position.Close();
                    }
                }
            }
        }

        public double GetValue(Bar bar)
        {
            return ((bar.Close + bar.Low + bar.High) / 3) * bar.TickVolume;
        }
        private double CalculateVwap2(Bars Bars)
        {
            var l = Bars.LastBar.OpenTime;
            var mins = l.Hour * 60 + l.Minute;
            var toTake = mins / cTimeFrame;
            var bars = Bars.TakeLast((int)toTake + 1).ToArray();

            if (bars.Length == 0)
            {
                return 0;
            }
            var value = (bars.Select(GetValue).Sum() / bars.Sum(x => x.TickVolume));
            return (Bars.LastBar.Close / value - 1) * 100;
        }

        protected override void OnBar()
        {
            for (int i = 0; i < AllBars.Count; i++)
            {
                var vwap = CalculateVwap2(AllBars[i]);
                var macd = macdCrossOvers[i];

                DefaultRun(macd, vwap, AllSymbols[i]);
            }
        }

        void DefaultRun(MacdCrossOver MACD, double vwap, string symbol)
        {
            var m = MACD.Histogram.TakeLast(2).ToList();

            if (m[0] < 0 && m[1] > 0)
            {
                if (vwap < 0)
                {
                    Buy(Volume, symbol);
                }
            }
            else if (m[0] > 0 && m[1] < 0)
            {
                if (vwap > 0)
                {
                    Sell(Volume, symbol);
                }
            }

            if (vwap > 0)
            {
                Close(TradeType.Buy, symbol);
            }
            else
            {
                Close(TradeType.Sell, symbol);
            }
        }


        private void Close(TradeType tradeType, string symbol)
        {
            if (Flip)
            {
                tradeType = (TradeType)(Math.Abs((int)tradeType - 1));
            }
            foreach (var position in Positions.FindAll(Label + " " + symbol, symbol, tradeType))
            {
                ClosePosition(position);
            }
        }
        private void Buy(double size, string symbol)
        {
            Close(TradeType.Sell, symbol);
            {
                Open(TradeType.Buy, Math.Round(size, 2), symbol);
            }
        }
        private void Sell(double size, string symbol)
        {
            Close(TradeType.Buy, symbol);
            {
                Open(TradeType.Sell, Math.Round(size, 2), symbol);
            }
        }


        private void Open(TradeType tradeType, double size, string symbol)
        {
            if (Flip)
            {
                tradeType = (TradeType)(Math.Abs((int)tradeType - 1));
            }
            /*var ssize = Math.Round(size * (Account.Equity / 10000.0), 2);
            size = Math.Min(ssize, size);*/

            var position = Positions.Find(Label + " " + symbol, symbol, tradeType);
            var volumeInUnits = Symbols.GetSymbolInfo(symbol).QuantityToVolumeInUnits(size);

            if (position == null)
            {
                Print(size + " " + Account.Equity);
                ExecuteMarketOrder(tradeType, symbol, volumeInUnits, Label + " " + symbol);
            }
        }
    }
}