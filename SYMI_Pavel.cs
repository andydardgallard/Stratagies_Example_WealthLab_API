using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using WealthLab;
using WealthLab.Indicators;

namespace YuryShevelev.VDV.Strategies
{
    internal class SYMI_Pavel : WealthLab.WealthScript
    {

        private StrategyParameter _channelEnter; //период верхнего и нижнего канала для входа в позицию 
        private StrategyParameter _channalExit; // % ширины канала. Здесь не может быть больше 50 %
        private StrategyParameter _timeEnter; // время входа
        private StrategyParameter _timeExit; // через сколько баров выйти

        public SYMI_Pavel()
        {
            _channelEnter = CreateParameter("Период для входа: ", 300, 50, 1000, 5);
            _channalExit = CreateParameter("Период для выхода: ", 2000, 100, 5000, 5);
            _timeEnter = CreateParameter("Время входа: ", 18, 10, 23, 1);
            _timeExit = CreateParameter("Время вsхода: ", 168, 2, 672, 1);
        }

        protected override void Execute()
        {
            double channelEnter = _channelEnter.ValueInt; //Определяем период канала
            double channelExit = _channalExit.Value; //Определяем % ширины канала
            int timeEnter = _timeEnter.ValueInt;
            int timeExit = _timeExit.ValueInt;
            int firstValidValue = 1; // Первое значение свечки при которой существуют все индикаторы

            HideVolume();
            
            #region Monday
            string WeekDay = "Monday";

            #region Indicators
            DataSeries highlevelSL_mnd = new DataSeries(Bars, "highlevelSL_mnd");
            DataSeries lowlevelSL_mnd = new DataSeries(Bars, "lowlevelSL_mnd");

            DataSeries highLevelTP_mnd = new DataSeries(Bars, "highLevelTP_mnd");
            DataSeries lowLevelTP_mnd = new DataSeries(Bars, "lowLevelTP_mnd");

            for (int bar = firstValidValue; bar < Bars.Count; bar++)
            {
                if (Bars.Date[bar].TimeOfDay.Hours == timeEnter && Bars.Date[bar].TimeOfDay.Minutes == 0
                        && Bars.Date[bar].DayOfWeek.ToString() == WeekDay)
                {
                    highlevelSL_mnd[bar] = Open[bar] + channelEnter;
                    lowlevelSL_mnd[bar] = Open[bar] - channelEnter;
                    highLevelTP_mnd[bar] = Open[bar] + channelExit;
                    lowLevelTP_mnd[bar] = Open[bar] - channelExit;
                }
                else
                {
                    highlevelSL_mnd[bar] = highlevelSL_mnd[bar - 1];
                    lowlevelSL_mnd[bar] = lowlevelSL_mnd[bar - 1];
                    highLevelTP_mnd[bar] = highLevelTP_mnd[bar - 1];
                    lowLevelTP_mnd[bar] = lowLevelTP_mnd[bar - 1];
                }
            }

            DataSeries zone_mnd = Close - Close;
                //Рисуем "внешний" канал
            PlotSeriesFillBand(PricePane, highLevelTP_mnd, lowLevelTP_mnd, Color.Blue, Color.LightBlue, LineStyle.Solid, 1);
                //Рисуем "внутренний" канал
            PlotSeriesFillBand(PricePane, highlevelSL_mnd, lowlevelSL_mnd, Color.Red, Color.Pink, LineStyle.Dashed, 1);

            #endregion Indicators
            
            #region Enter

            for (int bar = firstValidValue; bar < Bars.Count - 1; bar++)
            {
                var pos_mnd = Positions.Where(a => a.EntrySignal == WeekDay);


                if (Low[bar] < lowLevelTP_mnd[bar]) zone_mnd[bar] = 0.5; // Зона 1
                else if (Low[bar] >= lowLevelTP_mnd[bar] && Low[bar] < lowlevelSL_mnd[bar])
                    zone_mnd[bar] = 1.5; // Зона 2
                else if (High[bar] >= highlevelSL_mnd[bar] && High[bar] < highLevelTP_mnd[bar])
                    zone_mnd[bar] = 3.5; // зона 4
                else if (High[bar] >= highLevelTP_mnd[bar]) zone_mnd[bar] = 4.5; // зона 5
                else if (Close[bar] >= lowlevelSL_mnd[bar] && Close[bar] < highlevelSL_mnd[bar])
                    zone_mnd[bar] = 2.5; // Зона 3

                if (pos_mnd.Any()) //если не первая сделка 
                {
                    if (zone_mnd[bar - 1] < 3 && zone_mnd[bar] > 3)
                    {
                        if (pos_mnd.Last().PositionType == PositionType.Long)
                        {
                            if (pos_mnd.Last().RiskStopLevel != lowlevelSL_mnd[bar])
                            {
                                RiskStopLevel = lowlevelSL_mnd[bar];
                                BuyAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Buy_mnd", bar + 1, true, Color.Green);
                            }
                        }
                        else
                        {
                            if (pos_mnd.Last().RiskStopLevel != highlevelSL_mnd[bar])
                            {
                                RiskStopLevel = lowlevelSL_mnd[bar];
                                BuyAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Buy_mnd", bar + 1, true, Color.Green);
                            }
                        }
                    }
                    else if (zone_mnd[bar - 1] > 2 && zone_mnd[bar] < 2)
                    {
                        if (pos_mnd.Last().PositionType == PositionType.Long)
                        {
                            if (pos_mnd.Last().RiskStopLevel != lowlevelSL_mnd[bar])
                            {
                                RiskStopLevel = highlevelSL_mnd[bar];
                                ShortAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Short_mnd", bar + 1, false, Color.Red);
                            }
                        }
                        else
                        {
                            if (pos_mnd.Last().RiskStopLevel != highlevelSL_mnd[bar])
                            {
                                RiskStopLevel = highlevelSL_mnd[bar];
                                ShortAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Short_mnd", bar + 1, false, Color.Red);
                            }
                        }
                    }
                }
                else
                {
                    if (zone_mnd[bar - 1] < 3 && zone_mnd[bar] > 3 && lowlevelSL_mnd[bar] > 0) //Если первая сделка
                    {
                        RiskStopLevel = lowlevelSL_mnd[bar];
                        BuyAtLimit(bar + 1, Close[bar], WeekDay);
                        AnnotateBar("Buy_mnd", bar + 1, true, Color.Green);
                    }
                    else if (zone_mnd[bar - 1] > 2 && zone_mnd[bar] < 2 && lowlevelSL_mnd[bar] > 0)
                    {
                        RiskStopLevel = highlevelSL_mnd[bar];
                        ShortAtLimit(bar + 1, Close[bar], WeekDay);
                        AnnotateBar("Short_mnd", bar + 1, false, Color.Red);
                    }
                }

                #endregion Enter

            #region Exit
                foreach (var element in pos_mnd)
                {
                    if (element.Active)
                    {
                        if (bar == (Bars.Count - 2))
                        {
                            ExitAtClose(bar + 1, element, "LastBarExit");
                            continue;
                        }

                        if (bar + 1 - element.EntryBar >= timeExit)
                        {
                            ExitAtLimit(bar + 1, element, Close[bar], "Time_Exit");
                        }

                        if (element.PositionType == PositionType.Long)
                        {
                            if (Low[bar] < element.EntryPrice - channelEnter*2)
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "SL");
                            }
                            else if (High[bar] > element.EntryPrice + (channelExit - channelEnter))
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "TP");
                            }
                        }
                        else if (element.PositionType == PositionType.Short) //если позиция короткая
                        {
                            if (High[bar] > element.EntryPrice + channelEnter*2)
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "SL");
                            }
                            else if (Low[bar] < element.EntryPrice - (channelExit - channelEnter))
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "TP");
                            }
                        }
                    }
                }
            }
            #endregion Exit
            #endregion Monday

            #region Tuesday
            WeekDay = "Tuesday";

            #region Indicators
            DataSeries highlevelSL_tue = new DataSeries(Bars, "highlevelSL_tue");
            DataSeries lowlevelSL_tue = new DataSeries(Bars, "lowlevelSL_tue");

            DataSeries highLevelTP_tue = new DataSeries(Bars, "highLevelTP_tue");
            DataSeries lowLevelTP_tue = new DataSeries(Bars, "lowLevelTP_tue");

            for (int bar = firstValidValue; bar < Bars.Count; bar++)
            {
                if (Bars.Date[bar].TimeOfDay.Hours == timeEnter && Bars.Date[bar].TimeOfDay.Minutes == 0
                        && Bars.Date[bar].DayOfWeek.ToString() == WeekDay)
                {
                    highlevelSL_tue[bar] = Open[bar] + channelEnter;
                    lowlevelSL_tue[bar] = Open[bar] - channelEnter;
                    highLevelTP_tue[bar] = Open[bar] + channelExit;
                    lowLevelTP_tue[bar] = Open[bar] - channelExit;
                }
                else
                {
                    highlevelSL_tue[bar] = highlevelSL_tue[bar - 1];
                    lowlevelSL_tue[bar] = lowlevelSL_tue[bar - 1];
                    highLevelTP_tue[bar] = highLevelTP_tue[bar - 1];
                    lowLevelTP_tue[bar] = lowLevelTP_tue[bar - 1];
                }
            }

            DataSeries zone_tue = Close - Close;
                
            ChartPane TuesdayPane;
            TuesdayPane = CreatePane(100, false, true);
            PlotSymbol(TuesdayPane, Bars, Color.Green, Color.Red);
            PlotSeriesFillBand(TuesdayPane, highLevelTP_tue, lowLevelTP_tue, Color.Blue, Color.LightBlue, LineStyle.Solid, 1);
            PlotSeriesFillBand(TuesdayPane, highlevelSL_tue, lowlevelSL_tue, Color.Red, Color.Pink, LineStyle.Dashed, 1);

            #endregion Indicators
            
            #region Enter

            for (int bar = firstValidValue; bar < Bars.Count - 1; bar++)
            {
                var pos_tue = Positions.Where(a => a.EntrySignal == WeekDay);

                if (Low[bar] < lowLevelTP_tue[bar]) zone_tue[bar] = 0.5; // Зона 1
                else if (Low[bar] >= lowLevelTP_tue[bar] && Low[bar] < lowlevelSL_tue[bar]) zone_tue[bar] = 1.5; // Зона 2
                else if (High[bar] >= highlevelSL_tue[bar] && High[bar] < highLevelTP_tue[bar]) zone_tue[bar] = 3.5; // зона 4
                else if (High[bar] >= highLevelTP_tue[bar]) zone_tue[bar] = 4.5; // зона 5
                else if (Close[bar] >= lowlevelSL_tue[bar] && Close[bar] < highlevelSL_tue[bar]) zone_tue[bar] = 2.5; // Зона 3

                if (pos_tue.Any()) //если не первая сделка 
                {
                    if (zone_tue[bar - 1] < 3 && zone_tue[bar] > 3)
                    {
                        if (pos_tue.Last().PositionType == PositionType.Long)
                        {
                            if (pos_tue.Last().RiskStopLevel != lowlevelSL_tue[bar])
                            {
                                RiskStopLevel = lowlevelSL_tue[bar];
                                BuyAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Buy_tue", bar + 1, true, Color.Green);
                                DrawCircle(TuesdayPane, 10, bar + 1, highlevelSL_tue[bar + 1], Color.DarkGreen, Color.DarkGreen, LineStyle.Solid, 2, false);
                            }
                        }
                        else
                        {
                            if (pos_tue.Last().RiskStopLevel != highlevelSL_tue[bar])
                            {
                                RiskStopLevel = lowlevelSL_tue[bar];
                                BuyAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Buy_tue", bar + 1, true, Color.Green);
                                DrawCircle(TuesdayPane, 10, bar + 1, highlevelSL_tue[bar + 1], Color.DarkGreen, Color.DarkGreen, LineStyle.Solid, 2, false);
                            }
                        }
                    }
                    else if (zone_tue[bar - 1] > 2 && zone_tue[bar] < 2)
                    {
                        if (pos_tue.Last().PositionType == PositionType.Long)
                        {
                            if (pos_tue.Last().RiskStopLevel != lowlevelSL_tue[bar])
                            {
                                RiskStopLevel = highlevelSL_tue[bar];
                                ShortAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Short_tue", bar + 1, false, Color.Red);
                                DrawCircle(TuesdayPane, 10, bar + 1, lowlevelSL_tue[bar + 1], Color.DarkRed, Color.DarkRed, LineStyle.Solid, 2, false);
                            }
                        }
                        else
                        {
                            if (pos_tue.Last().RiskStopLevel != highlevelSL_tue[bar])
                            {
                                RiskStopLevel = highlevelSL_tue[bar];
                                ShortAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Short_tue", bar + 1, false, Color.Red);
                                DrawCircle(TuesdayPane, 10, bar + 1, lowlevelSL_tue[bar + 1], Color.DarkRed, Color.DarkRed, LineStyle.Solid, 2, false);
                            }
                        }
                    }
                }
                else
                {
                    if (zone_tue[bar - 1] < 3 && zone_tue[bar] > 3 && lowlevelSL_tue[bar] > 0) //Если первая сделка
                    {
                        RiskStopLevel = lowlevelSL_tue[bar];
                        BuyAtLimit(bar + 1, Close[bar], WeekDay);
                        AnnotateBar("Buy_tue", bar + 1, true, Color.Green);
                        DrawCircle(TuesdayPane, 10, bar + 1, highlevelSL_tue[bar + 1], Color.DarkGreen, Color.DarkGreen, LineStyle.Solid, 2, false);
                    }
                    else if (zone_tue[bar - 1] > 2 && zone_tue[bar] < 2 && lowlevelSL_tue[bar] > 0)
                    {
                        RiskStopLevel = highlevelSL_tue[bar];
                        ShortAtLimit(bar + 1, Close[bar], WeekDay);
                        AnnotateBar("Short_tue", bar + 1, false, Color.Red);
                        DrawCircle(TuesdayPane, 10, bar + 1, lowlevelSL_tue[bar + 1], Color.DarkRed, Color.DarkRed, LineStyle.Solid, 2, false);
                    }
                }
            #endregion Enter

            #region Exit
                foreach (var element in pos_tue)
                {
                    if (element.Active)
                    {
                        if (bar == (Bars.Count - 2))
                        {
                            ExitAtClose(bar + 1, element, "LastBarExit");
                            continue;
                        }

                        if (bar + 1 - element.EntryBar >= timeExit)
                        {
                            ExitAtLimit(bar + 1, element, Close[bar], "Time_Exit");
                        }

                        if (element.PositionType == PositionType.Long)
                        {
                            if (Low[bar] < element.EntryPrice - channelEnter*2)
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "SL");
                            }
                            else if (High[bar] > element.EntryPrice + (channelExit - channelEnter))
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "TP");
                            }
                        }
                        else if (element.PositionType == PositionType.Short) //если позиция короткая
                        {
                            if (High[bar] > element.EntryPrice + channelEnter*2)
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "SL");
                            }
                            else if (Low[bar] < element.EntryPrice - (channelExit - channelEnter))
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "TP");
                            }
                        }
                    }
                }
            }
            #endregion Exit
            #endregion Tuesday

            #region Wednesday
            WeekDay = "Wednesday";

            #region Indicators
            DataSeries highlevelSL_wed = new DataSeries(Bars, "highlevelSL_wed");
            DataSeries lowlevelSL_wed = new DataSeries(Bars, "lowlevelSL_wed");

            DataSeries highLevelTP_wed = new DataSeries(Bars, "highLevelTP_wed");
            DataSeries lowLevelTP_wed = new DataSeries(Bars, "lowLevelTP_wed");

            for (int bar = firstValidValue; bar < Bars.Count; bar++)
            {
                if (Bars.Date[bar].TimeOfDay.Hours == timeEnter && Bars.Date[bar].TimeOfDay.Minutes == 0
                        && Bars.Date[bar].DayOfWeek.ToString() == WeekDay)
                {
                    highlevelSL_wed[bar] = Open[bar] + channelEnter;
                    lowlevelSL_wed[bar] = Open[bar] - channelEnter;
                    highLevelTP_wed[bar] = Open[bar] + channelExit;
                    lowLevelTP_wed[bar] = Open[bar] - channelExit;
                }
                else
                {
                    highlevelSL_wed[bar] = highlevelSL_wed[bar - 1];
                    lowlevelSL_wed[bar] = lowlevelSL_wed[bar - 1];
                    highLevelTP_wed[bar] = highLevelTP_wed[bar - 1];
                    lowLevelTP_wed[bar] = lowLevelTP_wed[bar - 1];
                }
            }

            DataSeries zone_wed = Close - Close;

            ChartPane WednesdayPane;
            WednesdayPane = CreatePane(100, false, true);
            PlotSymbol(WednesdayPane, Bars, Color.Green, Color.Red);
            PlotSeriesFillBand(WednesdayPane, highLevelTP_wed, lowLevelTP_wed, Color.Blue, Color.LightBlue, LineStyle.Solid, 1);
            PlotSeriesFillBand(WednesdayPane, highlevelSL_wed, lowlevelSL_wed, Color.Red, Color.Pink, LineStyle.Dashed, 1);

            #endregion Indicators

            #region Enter

            for (int bar = firstValidValue; bar < Bars.Count - 1; bar++)
            {
                var pos_wed = Positions.Where(a => a.EntrySignal == WeekDay);

                if (Low[bar] < lowLevelTP_wed[bar]) zone_wed[bar] = 0.5; // Зона 1
                else if (Low[bar] >= lowLevelTP_wed[bar] && Low[bar] < lowlevelSL_wed[bar]) zone_wed[bar] = 1.5; // Зона 2
                else if (High[bar] >= highlevelSL_wed[bar] && High[bar] < highLevelTP_wed[bar]) zone_wed[bar] = 3.5; // зона 4
                else if (High[bar] >= highLevelTP_wed[bar]) zone_wed[bar] = 4.5; // зона 5
                else if (Close[bar] >= lowlevelSL_wed[bar] && Close[bar] < highlevelSL_wed[bar]) zone_wed[bar] = 2.5; // Зона 3

                if (pos_wed.Any()) //если не первая сделка 
                {
                    if (zone_wed[bar - 1] < 3 && zone_wed[bar] > 3)
                    {
                        if (pos_wed.Last().PositionType == PositionType.Long)
                        {
                            if (pos_wed.Last().RiskStopLevel != lowlevelSL_wed[bar])
                            {
                                RiskStopLevel = lowlevelSL_wed[bar];
                                BuyAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Buy_wed", bar + 1, true, Color.Green);
                                DrawCircle(WednesdayPane, 10, bar + 1, highlevelSL_wed[bar + 1], Color.DarkGreen, Color.DarkGreen, LineStyle.Solid, 2, false);
                            }
                        }
                        else
                        {
                            if (pos_wed.Last().RiskStopLevel != highlevelSL_wed[bar])
                            {
                                RiskStopLevel = lowlevelSL_wed[bar];
                                BuyAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Buy_wed", bar + 1, true, Color.Green);
                                DrawCircle(WednesdayPane, 10, bar + 1, highlevelSL_wed[bar + 1], Color.DarkGreen, Color.DarkGreen, LineStyle.Solid, 2, false);
                            }
                        }
                    }
                    else if (zone_wed[bar - 1] > 2 && zone_wed[bar] < 2)
                    {
                        if (pos_wed.Last().PositionType == PositionType.Long)
                        {
                            if (pos_wed.Last().RiskStopLevel != lowlevelSL_wed[bar])
                            {
                                RiskStopLevel = highlevelSL_wed[bar];
                                ShortAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Short_wed", bar + 1, false, Color.Red);
                                DrawCircle(WednesdayPane, 10, bar + 1, lowlevelSL_wed[bar + 1], Color.DarkRed, Color.DarkRed, LineStyle.Solid, 2, false);
                            }
                        }
                        else
                        {
                            if (pos_wed.Last().RiskStopLevel != highlevelSL_wed[bar])
                            {
                                RiskStopLevel = highlevelSL_wed[bar];
                                ShortAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Short_wed", bar + 1, false, Color.Red);
                                DrawCircle(WednesdayPane, 10, bar + 1, lowlevelSL_wed[bar + 1], Color.DarkRed, Color.DarkRed, LineStyle.Solid, 2, false);
                            }
                        }
                    }
                }
                else
                {
                    if (zone_wed[bar - 1] < 3 && zone_wed[bar] > 3 && lowlevelSL_wed[bar] > 0) //Если первая сделка
                    {
                        RiskStopLevel = lowlevelSL_wed[bar];
                        BuyAtLimit(bar + 1, Close[bar], WeekDay);
                        AnnotateBar("Buy_wed", bar + 1, true, Color.Green);
                        DrawCircle(WednesdayPane, 10, bar + 1, highlevelSL_wed[bar + 1], Color.DarkGreen, Color.DarkGreen, LineStyle.Solid, 2, false);
                    }
                    else if (zone_wed[bar - 1] > 2 && zone_wed[bar] < 2 && lowlevelSL_wed[bar] > 0)
                    {
                        RiskStopLevel = highlevelSL_wed[bar];
                        ShortAtLimit(bar + 1, Close[bar], WeekDay);
                        AnnotateBar("Short_wed", bar + 1, false, Color.Red);
                        DrawCircle(WednesdayPane, 10, bar + 1, lowlevelSL_wed[bar + 1], Color.DarkRed, Color.DarkRed, LineStyle.Solid, 2, false);
                    }
                }
            #endregion Enter

            #region Exit
                foreach (var element in pos_wed)
                {
                    if (element.Active)
                    {
                        if (bar == (Bars.Count - 2))
                        {
                            ExitAtClose(bar + 1, element, "LastBarExit");
                            continue;
                        }

                        if (bar + 1 - element.EntryBar >= timeExit)
                        {
                            ExitAtLimit(bar + 1, element, Close[bar], "Time_Exit");
                        }

                        if (element.PositionType == PositionType.Long)
                        {
                            if (Low[bar] < element.EntryPrice - channelEnter * 2)
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "SL");
                            }
                            else if (High[bar] > element.EntryPrice + (channelExit - channelEnter))
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "TP");
                            }
                        }
                        else if (element.PositionType == PositionType.Short) //если позиция короткая
                        {
                            if (High[bar] > element.EntryPrice + channelEnter * 2)
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "SL");
                            }
                            else if (Low[bar] < element.EntryPrice - (channelExit - channelEnter))
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "TP");
                            }
                        }
                    }
                }
            }
            #endregion Exit
            #endregion Wednesday

            #region Thursday
            WeekDay = "Thursday";

            #region Indicators
            DataSeries highlevelSL_thu = new DataSeries(Bars, "highlevelSL_thu");
            DataSeries lowlevelSL_thu = new DataSeries(Bars, "lowlevelSL_thu");

            DataSeries highLevelTP_thu = new DataSeries(Bars, "highLevelTP_thu");
            DataSeries lowLevelTP_thu = new DataSeries(Bars, "lowLevelTP_thu");

            for (int bar = firstValidValue; bar < Bars.Count; bar++)
            {
                if (Bars.Date[bar].TimeOfDay.Hours == timeEnter && Bars.Date[bar].TimeOfDay.Minutes == 0
                        && Bars.Date[bar].DayOfWeek.ToString() == WeekDay)
                {
                    highlevelSL_thu[bar] = Open[bar] + channelEnter;
                    lowlevelSL_thu[bar] = Open[bar] - channelEnter;
                    highLevelTP_thu[bar] = Open[bar] + channelExit;
                    lowLevelTP_thu[bar] = Open[bar] - channelExit;
                }
                else
                {
                    highlevelSL_thu[bar] = highlevelSL_thu[bar - 1];
                    lowlevelSL_thu[bar] = lowlevelSL_thu[bar - 1];
                    highLevelTP_thu[bar] = highLevelTP_thu[bar - 1];
                    lowLevelTP_thu[bar] = lowLevelTP_thu[bar - 1];
                }
            }

            DataSeries zone_thu = Close - Close;

            ChartPane ThursdayPane;
            ThursdayPane = CreatePane(100, false, true);
            PlotSymbol(ThursdayPane, Bars, Color.Green, Color.Red);
            PlotSeriesFillBand(ThursdayPane, highLevelTP_thu, lowLevelTP_thu, Color.Blue, Color.LightBlue, LineStyle.Solid, 1);
            PlotSeriesFillBand(ThursdayPane, highlevelSL_thu, lowlevelSL_thu, Color.Red, Color.Pink, LineStyle.Dashed, 1);

            #endregion Indicators

            #region Enter

            for (int bar = firstValidValue; bar < Bars.Count - 1; bar++)
            {
                var pos_thu = Positions.Where(a => a.EntrySignal == WeekDay);

                if (Low[bar] < lowLevelTP_thu[bar]) zone_thu[bar] = 0.5; // Зона 1
                else if (Low[bar] >= lowLevelTP_thu[bar] && Low[bar] < lowlevelSL_thu[bar]) zone_thu[bar] = 1.5; // Зона 2
                else if (High[bar] >= highlevelSL_thu[bar] && High[bar] < highLevelTP_thu[bar]) zone_thu[bar] = 3.5; // зона 4
                else if (High[bar] >= highLevelTP_thu[bar]) zone_thu[bar] = 4.5; // зона 5
                else if (Close[bar] >= lowlevelSL_thu[bar] && Close[bar] < highlevelSL_thu[bar]) zone_thu[bar] = 2.5; // Зона 3

                if (pos_thu.Any()) //если не первая сделка 
                {
                    if (zone_thu[bar - 1] < 3 && zone_thu[bar] > 3)
                    {
                        if (pos_thu.Last().PositionType == PositionType.Long)
                        {
                            if (pos_thu.Last().RiskStopLevel != lowlevelSL_thu[bar])
                            {
                                RiskStopLevel = lowlevelSL_thu[bar];
                                BuyAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Buy_thu", bar + 1, true, Color.Green);
                                DrawCircle(ThursdayPane, 10, bar + 1, highlevelSL_thu[bar + 1], Color.DarkGreen, Color.DarkGreen, LineStyle.Solid, 2, false);
                            }
                        }
                        else
                        {
                            if (pos_thu.Last().RiskStopLevel != highlevelSL_thu[bar])
                            {
                                RiskStopLevel = lowlevelSL_thu[bar];
                                BuyAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Buy_thu", bar + 1, true, Color.Green);
                                DrawCircle(ThursdayPane, 10, bar + 1, highlevelSL_thu[bar + 1], Color.DarkGreen, Color.DarkGreen, LineStyle.Solid, 2, false);
                            }
                        }
                    }
                    else if (zone_thu[bar - 1] > 2 && zone_thu[bar] < 2)
                    {
                        if (pos_thu.Last().PositionType == PositionType.Long)
                        {
                            if (pos_thu.Last().RiskStopLevel != lowlevelSL_thu[bar])
                            {
                                RiskStopLevel = highlevelSL_thu[bar];
                                ShortAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Short_thu", bar + 1, false, Color.Red);
                                DrawCircle(ThursdayPane, 10, bar + 1, lowlevelSL_thu[bar + 1], Color.DarkRed, Color.DarkRed, LineStyle.Solid, 2, false);
                            }
                        }
                        else
                        {
                            if (pos_thu.Last().RiskStopLevel != highlevelSL_thu[bar])
                            {
                                RiskStopLevel = highlevelSL_thu[bar];
                                ShortAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Short_thu", bar + 1, false, Color.Red);
                                DrawCircle(ThursdayPane, 10, bar + 1, lowlevelSL_thu[bar + 1], Color.DarkRed, Color.DarkRed, LineStyle.Solid, 2, false);
                            }
                        }
                    }
                }
                else
                {
                    if (zone_thu[bar - 1] < 3 && zone_thu[bar] > 3 && lowlevelSL_thu[bar] > 0) //Если первая сделка
                    {
                        RiskStopLevel = lowlevelSL_thu[bar];
                        BuyAtLimit(bar + 1, Close[bar], WeekDay);
                        AnnotateBar("Buy_thu", bar + 1, true, Color.Green);
                        DrawCircle(ThursdayPane, 10, bar + 1, highlevelSL_thu[bar + 1], Color.DarkGreen, Color.DarkGreen, LineStyle.Solid, 2, false);
                    }
                    else if (zone_thu[bar - 1] > 2 && zone_thu[bar] < 2 && lowlevelSL_thu[bar] > 0)
                    {
                        RiskStopLevel = highlevelSL_thu[bar];
                        ShortAtLimit(bar + 1, Close[bar], WeekDay);
                        AnnotateBar("Short_thu", bar + 1, false, Color.Red);
                        DrawCircle(ThursdayPane, 10, bar + 1, lowlevelSL_thu[bar + 1], Color.DarkRed, Color.DarkRed, LineStyle.Solid, 2, false);
                    }
                }
            #endregion Enter

            #region Exit
                foreach (var element in pos_thu)
                {
                    if (element.Active)
                    {
                        if (bar == (Bars.Count - 2))
                        {
                            ExitAtClose(bar + 1, element, "LastBarExit");
                            continue;
                        }

                        if (bar + 1 - element.EntryBar >= timeExit)
                        {
                            ExitAtLimit(bar + 1, element, Close[bar], "Time_Exit");
                        }

                        if (element.PositionType == PositionType.Long)
                        {
                            if (Low[bar] < element.EntryPrice - channelEnter * 2)
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "SL");
                            }
                            else if (High[bar] > element.EntryPrice + (channelExit - channelEnter))
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "TP");
                            }
                        }
                        else if (element.PositionType == PositionType.Short) //если позиция короткая
                        {
                            if (High[bar] > element.EntryPrice + channelEnter * 2)
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "SL");
                            }
                            else if (Low[bar] < element.EntryPrice - (channelExit - channelEnter))
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "TP");
                            }
                        }
                    }
                }
            }
                #endregion Exit
            #endregion Thursday

            #region Friday
            WeekDay = "Friday";

            #region Indicators
            DataSeries highlevelSL_fri = new DataSeries(Bars, "highlevelSL_fri");
            DataSeries lowlevelSL_fri = new DataSeries(Bars, "lowlevelSL_fri");

            DataSeries highLevelTP_fri = new DataSeries(Bars, "highLevelTP_fri");
            DataSeries lowLevelTP_fri = new DataSeries(Bars, "lowLevelTP_fri");

            for (int bar = firstValidValue; bar < Bars.Count; bar++)
            {
                if (Bars.Date[bar].TimeOfDay.Hours == timeEnter && Bars.Date[bar].TimeOfDay.Minutes == 0
                        && Bars.Date[bar].DayOfWeek.ToString() == WeekDay)
                {
                    highlevelSL_fri[bar] = Open[bar] + channelEnter;
                    lowlevelSL_fri[bar] = Open[bar] - channelEnter;
                    highLevelTP_fri[bar] = Open[bar] + channelExit;
                    lowLevelTP_fri[bar] = Open[bar] - channelExit;
                }
                else
                {
                    highlevelSL_fri[bar] = highlevelSL_fri[bar - 1];
                    lowlevelSL_fri[bar] = lowlevelSL_fri[bar - 1];
                    highLevelTP_fri[bar] = highLevelTP_fri[bar - 1];
                    lowLevelTP_fri[bar] = lowLevelTP_fri[bar - 1];
                }
            }

            DataSeries zone_fri = Close - Close;

            ChartPane FridayPane;
            FridayPane = CreatePane(100, false, true);
            PlotSymbol(FridayPane, Bars, Color.Green, Color.Red);
            PlotSeriesFillBand(FridayPane, highLevelTP_fri, lowLevelTP_fri, Color.Blue, Color.LightBlue, LineStyle.Solid, 1);
            PlotSeriesFillBand(FridayPane, highlevelSL_fri, lowlevelSL_fri, Color.Red, Color.Pink, LineStyle.Dashed, 1);

            #endregion Indicators

            #region Enter

            for (int bar = firstValidValue; bar < Bars.Count - 1; bar++)
            {
                var pos_fri = Positions.Where(a => a.EntrySignal == WeekDay);

                if (Low[bar] < lowLevelTP_fri[bar]) zone_fri[bar] = 0.5; // Зона 1
                else if (Low[bar] >= lowLevelTP_fri[bar] && Low[bar] < lowlevelSL_fri[bar]) zone_fri[bar] = 1.5; // Зона 2
                else if (High[bar] >= highlevelSL_fri[bar] && High[bar] < highLevelTP_fri[bar]) zone_fri[bar] = 3.5; // зона 4
                else if (High[bar] >= highLevelTP_fri[bar]) zone_fri[bar] = 4.5; // зона 5
                else if (Close[bar] >= lowlevelSL_fri[bar] && Close[bar] < highlevelSL_fri[bar]) zone_fri[bar] = 2.5; // Зона 3

                if (pos_fri.Any()) //если не первая сделка 
                {
                    if (zone_fri[bar - 1] < 3 && zone_fri[bar] > 3)
                    {
                        if (pos_fri.Last().PositionType == PositionType.Long)
                        {
                            if (pos_fri.Last().RiskStopLevel != lowlevelSL_fri[bar])
                            {
                                RiskStopLevel = lowlevelSL_fri[bar];
                                BuyAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Buy_fri", bar + 1, true, Color.Green);
                                DrawCircle(FridayPane, 10, bar + 1, highlevelSL_fri[bar + 1], Color.DarkGreen, Color.DarkGreen, LineStyle.Solid, 2, false);
                            }
                        }
                        else
                        {
                            if (pos_fri.Last().RiskStopLevel != highlevelSL_fri[bar])
                            {
                                RiskStopLevel = lowlevelSL_fri[bar];
                                BuyAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Buy_fri", bar + 1, true, Color.Green);
                                DrawCircle(FridayPane, 10, bar + 1, highlevelSL_fri[bar + 1], Color.DarkGreen, Color.DarkGreen, LineStyle.Solid, 2, false);
                            }
                        }
                    }
                    else if (zone_fri[bar - 1] > 2 && zone_fri[bar] < 2)
                    {
                        if (pos_fri.Last().PositionType == PositionType.Long)
                        {
                            if (pos_fri.Last().RiskStopLevel != lowlevelSL_fri[bar])
                            {
                                RiskStopLevel = highlevelSL_fri[bar];
                                ShortAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Short_fri", bar + 1, false, Color.Red);
                                DrawCircle(FridayPane, 10, bar + 1, lowlevelSL_fri[bar + 1], Color.DarkRed, Color.DarkRed, LineStyle.Solid, 2, false);
                            }
                        }
                        else
                        {
                            if (pos_fri.Last().RiskStopLevel != highlevelSL_fri[bar])
                            {
                                RiskStopLevel = highlevelSL_fri[bar];
                                ShortAtLimit(bar + 1, Close[bar], WeekDay);
                                AnnotateBar("Short_fri", bar + 1, false, Color.Red);
                                DrawCircle(FridayPane, 10, bar + 1, lowlevelSL_fri[bar + 1], Color.DarkRed, Color.DarkRed, LineStyle.Solid, 2, false);
                            }
                        }
                    }
                }
                else
                {
                    if (zone_fri[bar - 1] < 3 && zone_fri[bar] > 3 && lowlevelSL_fri[bar] > 0) //Если первая сделка
                    {
                        RiskStopLevel = lowlevelSL_fri[bar];
                        BuyAtLimit(bar + 1, Close[bar], WeekDay);
                        AnnotateBar("Buy_fri", bar + 1, true, Color.Green);
                        DrawCircle(FridayPane, 10, bar + 1, highlevelSL_fri[bar + 1], Color.DarkGreen, Color.DarkGreen, LineStyle.Solid, 2, false);
                    }
                    else if (zone_fri[bar - 1] > 2 && zone_fri[bar] < 2 && lowlevelSL_fri[bar] > 0)
                    {
                        RiskStopLevel = highlevelSL_fri[bar];
                        ShortAtLimit(bar + 1, Close[bar], WeekDay);
                        AnnotateBar("Short_fri", bar + 1, false, Color.Red);
                        DrawCircle(FridayPane, 10, bar + 1, lowlevelSL_fri[bar + 1], Color.DarkRed, Color.DarkRed, LineStyle.Solid, 2, false);
                    }
                }
            #endregion Enter

            #region Exit
                foreach (var element in pos_fri)
                {
                    if (element.Active)
                    {
                        if (bar == (Bars.Count - 2))
                        {
                            ExitAtClose(bar + 1, element, "LastBarExit");
                            continue;
                        }

                        if (bar + 1 - element.EntryBar >= timeExit)
                        {
                            ExitAtLimit(bar + 1, element, Close[bar], "Time_Exit");
                        }

                        if (element.PositionType == PositionType.Long)
                        {
                            if (Low[bar] < element.EntryPrice - channelEnter * 2)
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "SL");
                            }
                            else if (High[bar] > element.EntryPrice + (channelExit - channelEnter))
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "TP");
                            }
                        }
                        else if (element.PositionType == PositionType.Short) //если позиция короткая
                        {
                            if (High[bar] > element.EntryPrice + channelEnter * 2)
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "SL");
                            }
                            else if (Low[bar] < element.EntryPrice - (channelExit - channelEnter))
                            {
                                ExitAtLimit(bar + 1, element, Close[bar], "TP");
                            }
                        }
                    }
                }
            }
                #endregion Exit
            #endregion Friday

        }
    }
}