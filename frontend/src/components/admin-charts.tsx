import { useEffect, useRef, useState } from "react";
import { init, use, type EChartsType } from "echarts/core";
import { BarChart, PieChart } from "echarts/charts";
import {
  AriaComponent,
  DataZoomComponent,
  GridComponent,
  LegendComponent,
  TitleComponent,
  TooltipComponent,
} from "echarts/components";
import { SVGRenderer } from "echarts/renderers";
import type { EChartsOption } from "echarts";
import { shortDate } from "../domain";

use([
  BarChart,
  PieChart,
  GridComponent,
  LegendComponent,
  TooltipComponent,
  DataZoomComponent,
  TitleComponent,
  AriaComponent,
  SVGRenderer,
]);

const NAVY = "#0b0744";
const GOLD = "#d3ac26";
const MUTED = "#646b7c";
const LINE = "#eeedf4";
const tooltip: EChartsOption["tooltip"] = {
  renderMode: "richText",
  confine: true,
  backgroundColor: "#fff",
  borderColor: "#dde0e9",
  borderWidth: 1,
  padding: 12,
  textStyle: { color: NAVY, fontSize: 12, fontFamily: "Inter, sans-serif" },
};

function EChart({
  option,
  description,
  className = "",
  instanceRef,
}: {
  option: EChartsOption;
  description: string;
  className?: string;
  instanceRef?: { current: EChartsType | null };
}) {
  const host = useRef<HTMLDivElement>(null);
  const instance = useRef<EChartsType | null>(null);
  const latestOption = useRef(option);
  latestOption.current = option;

  useEffect(() => {
    const element = host.current!;
    let chart: EChartsType | null = null;
    const motion = matchMedia("(prefers-reduced-motion: reduce)");
    function apply() {
      chart?.setOption(
        { ...latestOption.current, animation: !motion.matches },
        { replaceMerge: ["series"] },
      );
    }
    function resize() {
      if (chart) {
        chart.resize();
        return;
      }
      if (!element.clientWidth || !element.clientHeight) return;
      chart = init(element, undefined, { renderer: "svg" });
      instance.current = chart;
      if (instanceRef) instanceRef.current = chart;
      apply();
    }
    resize();
    motion.addEventListener("change", apply);
    const observer = new ResizeObserver(resize);
    observer.observe(element);
    return () => {
      observer.disconnect();
      motion.removeEventListener("change", apply);
      if (instanceRef) instanceRef.current = null;
      instance.current = null;
      chart?.dispose();
    };
  }, [instanceRef]);

  useEffect(() => {
    instance.current?.setOption(
      {
        ...option,
        animation: !matchMedia("(prefers-reduced-motion: reduce)").matches,
      },
      { replaceMerge: ["series"] },
    );
  }, [option]);

  return (
    <div
      ref={host}
      className={`echart ${className}`}
      role="img"
      aria-label={description}
    />
  );
}

interface Daily {
  date: string;
  total: number;
  cancelled: number;
}
export function ActivityChart({ days }: { days: Daily[] }) {
  const [selected, setSelected] = useState({
    Reservas: true,
    Cancelaciones: true,
  });
  const chart = useRef<EChartsType | null>(null);
  const zoom = days.length > 7;
  const option: EChartsOption = {
    color: [NAVY, GOLD],
    textStyle: { fontFamily: "Inter, sans-serif" },
    aria: {
      enabled: true,
      label: { description: "Reservas y cancelaciones por fecha de sesión." },
    },
    legend: { show: false, selected },
    grid: { top: 20, left: 40, right: 12, bottom: zoom ? 72 : 36 },
    tooltip: {
      ...tooltip,
      trigger: "axis",
      axisPointer: { type: "shadow" },
      valueFormatter: (value) => `${value} reservas`,
    },
    xAxis: {
      type: "category",
      data: days.map((day) => day.date),
      axisLine: { lineStyle: { color: LINE } },
      axisTick: { show: false },
      axisLabel: {
        color: MUTED,
        fontSize: 11,
        hideOverlap: true,
        formatter: (date) =>
          shortDate(date, { day: "2-digit", month: "short" }).replaceAll(
            ".",
            "",
          ),
      },
      axisPointer: {
        label: { formatter: (params) => shortDate(String(params.value)) },
      },
    },
    yAxis: {
      type: "value",
      min: 0,
      minInterval: 1,
      max: (value) => Math.max(2, value.max),
      axisLabel: { color: MUTED, fontSize: 11 },
      splitLine: { lineStyle: { color: LINE } },
    },
    dataZoom: zoom
      ? [
          {
            type: "slider",
            height: 18,
            bottom: 10,
            borderColor: "transparent",
            backgroundColor: "#f5f4f9",
            fillerColor: "#0b07441a",
            handleStyle: { color: NAVY },
            moveHandleSize: 0,
            brushSelect: false,
            textStyle: { color: MUTED },
            labelFormatter: (_value, date) =>
              shortDate(String(date), { day: "numeric", month: "short" }),
          },
          {
            type: "inside",
            zoomOnMouseWheel: "ctrl",
            moveOnMouseWheel: false,
            preventDefaultMouseMove: false,
          },
        ]
      : [],
    series: [
      {
        name: "Reservas",
        type: "bar",
        data: days.map((day) => day.total),
        barMaxWidth: 24,
        itemStyle: { borderRadius: [4, 4, 0, 0] },
        emphasis: { focus: "series" },
      },
      {
        name: "Cancelaciones",
        type: "bar",
        data: days.map((day) => day.cancelled),
        barMaxWidth: 16,
        itemStyle: { borderRadius: [4, 4, 0, 0] },
        emphasis: { focus: "series" },
      },
    ],
  };
  return (
    <div className="activity-chart-content">
      <div className="chart-legend" aria-label="Series del gráfico">
        {(["Reservas", "Cancelaciones"] as const).map((name) => (
          <button
            key={name}
            aria-pressed={selected[name]}
            onClick={() =>
              setSelected((value) => ({ ...value, [name]: !value[name] }))
            }
          >
            <i style={{ backgroundColor: name === "Reservas" ? NAVY : GOLD }} />
            {name}
          </button>
        ))}
      </div>
      <EChart
        option={option}
        description="Reservas y cancelaciones por fecha de sesión"
        instanceRef={chart}
        className={zoom ? "activity-echart zoomable" : "activity-echart"}
      />
      {zoom && (
        <div className="chart-zoom-controls">
          <button
            onClick={() =>
              chart.current?.dispatchAction({
                type: "dataZoom",
                startValue: days[Math.max(0, days.length - 7)].date,
                endValue: days.at(-1)!.date,
              })
            }
          >
            Ampliar últimos 7 días
          </button>
          <button
            onClick={() =>
              chart.current?.dispatchAction({
                type: "dataZoom",
                start: 0,
                end: 100,
              })
            }
          >
            Restablecer zoom
          </button>
        </div>
      )}
      <details className="chart-data">
        <summary>Ver datos</summary>
        <div className="table-scroll">
          <table>
            <caption>Reservas por fecha de sesión</caption>
            <thead>
              <tr>
                <th scope="col">Fecha</th>
                <th scope="col">Total</th>
                <th scope="col">Canceladas</th>
              </tr>
            </thead>
            <tbody>
              {days.map((day) => (
                <tr key={day.date}>
                  <th scope="row">
                    {shortDate(day.date, { day: "numeric", month: "short" })}
                  </th>
                  <td>{day.total}</td>
                  <td>{day.cancelled}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </details>
    </div>
  );
}

export function OccupancyChart({
  present,
  capacity,
}: {
  present: number;
  capacity: number;
}) {
  const available = Math.max(0, capacity - present);
  const option: EChartsOption = {
    color: [NAVY, LINE],
    aria: {
      enabled: true,
      label: {
        description: `${present} personas en sala. ${available} plazas libres de ${capacity}.`,
      },
    },
    tooltip: {
      ...tooltip,
      trigger: "item",
      valueFormatter: (value) => `${value} personas`,
    },
    title: {
      text: String(present),
      subtext: "en sala",
      left: "center",
      top: "34%",
      itemGap: 5,
      textStyle: {
        fontFamily: "Inter, sans-serif",
        fontSize: 40,
        fontWeight: 600,
        color: NAVY,
      },
      subtextStyle: {
        color: MUTED,
        fontFamily: "Inter, sans-serif",
        fontSize: 12,
      },
    },
    series: [
      {
        type: "pie",
        radius: ["76%", "88%"],
        center: ["50%", "50%"],
        label: { show: false },
        emphasis: { scaleSize: 3 },
        data: [
          { name: "En sala", value: present },
          { name: "Plazas libres", value: available },
        ],
      },
    ],
  };
  return (
    <EChart
      option={option}
      description={`${present} personas en sala. Capacidad de ${capacity} personas.`}
      className="occupancy-echart"
    />
  );
}

export function DemandChart({
  demand,
}: {
  demand: { start: string; count: number }[];
}) {
  const items = demand.slice(0, 5);
  const option: EChartsOption = {
    color: [NAVY],
    textStyle: { fontFamily: "Inter, sans-serif" },
    aria: {
      enabled: true,
      label: {
        description: "Cantidad de reservas no canceladas por hora de inicio.",
      },
    },
    grid: { top: 16, bottom: 14, left: 55, right: 40 },
    tooltip: {
      ...tooltip,
      trigger: "axis",
      axisPointer: { type: "shadow" },
      valueFormatter: (value) => `${value} reservas`,
    },
    xAxis: { type: "value", show: false, minInterval: 1 },
    yAxis: {
      type: "category",
      inverse: true,
      data: items.map((item) => item.start),
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: { color: MUTED, fontSize: 12 },
    },
    series: [
      {
        name: "Reservas",
        type: "bar",
        barMaxWidth: 12,
        showBackground: true,
        backgroundStyle: { color: LINE, borderRadius: 6 },
        itemStyle: { borderRadius: 6 },
        label: { show: true, position: "right", color: NAVY, fontSize: 12 },
        data: items.map((item) => item.count),
      },
    ],
  };
  return (
    <>
      <EChart
        option={option}
        description="Horarios más solicitados"
        className="demand-echart"
      />
      <details className="chart-data">
        <summary>Ver datos</summary>
        <table>
          <caption>Reservas no canceladas por horario</caption>
          <thead>
            <tr>
              <th scope="col">Horario</th>
              <th scope="col">Reservas</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.start}>
                <th scope="row">{item.start}</th>
                <td>{item.count}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </details>
    </>
  );
}
