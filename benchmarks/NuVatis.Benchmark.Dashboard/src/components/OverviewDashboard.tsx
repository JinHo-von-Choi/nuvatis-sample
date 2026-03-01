import { BenchmarkResult, CategorySummary } from '../types';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
  RadarChart,
  PolarGrid,
  PolarAngleAxis,
  PolarRadiusAxis,
  Radar,
  LineChart,
  Line
} from 'recharts';

interface Props {
  data: BenchmarkResult[];
}

export default function OverviewDashboard({ data }: Props) {
  // 카테고리별 요약 통계 계산
  const categorySummaries = calculateCategorySummaries(data);
  const ormMetrics = calculateOrmMetrics(data);
  const winnerCounts = calculateWinnerCounts(data);

  return (
    <div className="space-y-8">
      {/* 핵심 메트릭 카드 */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        {ormMetrics.map(metrics => (
          <div
            key={metrics.orm}
            className={`p-6 rounded-xl border-2 ${getOrmBorderColor(metrics.orm)} bg-slate-800`}
          >
            <div className="flex items-center gap-3 mb-4">
              <div className={`w-4 h-4 rounded-full ${getOrmColor(metrics.orm)}`}></div>
              <h3 className="text-xl font-bold">{metrics.orm}</h3>
            </div>
            <div className="space-y-3">
              <div>
                <p className="text-slate-400 text-sm">평균 응답 시간</p>
                <p className="text-2xl font-bold">{metrics.avgLatency.toFixed(2)} ms</p>
              </div>
              <div>
                <p className="text-slate-400 text-sm">처리량</p>
                <p className="text-xl font-semibold">{metrics.avgThroughput.toFixed(0)} ops/s</p>
              </div>
              <div>
                <p className="text-slate-400 text-sm">평균 메모리</p>
                <p className="text-xl font-semibold">{metrics.avgMemory.toFixed(1)} MB</p>
              </div>
              <div>
                <p className="text-slate-400 text-sm">승리 시나리오</p>
                <p className="text-xl font-semibold">
                  {winnerCounts[metrics.orm.toLowerCase()] || 0} / 60
                </p>
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* 레이더 차트: 카테고리별 성능 비교 */}
      <div className="bg-slate-800 p-6 rounded-xl">
        <h2 className="text-2xl font-bold mb-6">카테고리별 성능 비교 (레이더 차트)</h2>
        <ResponsiveContainer width="100%" height={400}>
          <RadarChart data={categorySummaries}>
            <PolarGrid stroke="#475569" />
            <PolarAngleAxis dataKey="category" stroke="#94a3b8" />
            <PolarRadiusAxis stroke="#94a3b8" />
            <Radar
              name="NuVatis"
              dataKey="nuvatis"
              stroke="#4F46E5"
              fill="#4F46E5"
              fillOpacity={0.3}
            />
            <Radar
              name="Dapper"
              dataKey="dapper"
              stroke="#10B981"
              fill="#10B981"
              fillOpacity={0.3}
            />
            <Radar
              name="EF Core"
              dataKey="efcore"
              stroke="#F59E0B"
              fill="#F59E0B"
              fillOpacity={0.3}
            />
            <Legend />
          </RadarChart>
        </ResponsiveContainer>
      </div>

      {/* 바 차트: 카테고리별 평균 응답 시간 */}
      <div className="bg-slate-800 p-6 rounded-xl">
        <h2 className="text-2xl font-bold mb-6">카테고리별 평균 응답 시간</h2>
        <ResponsiveContainer width="100%" height={300}>
          <BarChart data={categorySummaries}>
            <CartesianGrid strokeDasharray="3 3" stroke="#475569" />
            <XAxis dataKey="category" stroke="#94a3b8" />
            <YAxis stroke="#94a3b8" label={{ value: 'ms', angle: -90, position: 'insideLeft' }} />
            <Tooltip
              contentStyle={{ backgroundColor: '#1e293b', border: '1px solid #475569' }}
            />
            <Legend />
            <Bar dataKey="nuvatis" fill="#4F46E5" name="NuVatis" />
            <Bar dataKey="dapper" fill="#10B981" name="Dapper" />
            <Bar dataKey="efcore" fill="#F59E0B" name="EF Core" />
          </BarChart>
        </ResponsiveContainer>
      </div>

      {/* 라인 차트: 시나리오별 추세 */}
      <div className="bg-slate-800 p-6 rounded-xl">
        <h2 className="text-2xl font-bold mb-6">시나리오별 응답 시간 추세</h2>
        <ResponsiveContainer width="100%" height={300}>
          <LineChart data={getScenarioTrend(data)}>
            <CartesianGrid strokeDasharray="3 3" stroke="#475569" />
            <XAxis dataKey="scenario" stroke="#94a3b8" />
            <YAxis stroke="#94a3b8" label={{ value: 'ms', angle: -90, position: 'insideLeft' }} />
            <Tooltip
              contentStyle={{ backgroundColor: '#1e293b', border: '1px solid #475569' }}
            />
            <Legend />
            <Line
              type="monotone"
              dataKey="nuvatis"
              stroke="#4F46E5"
              name="NuVatis"
              strokeWidth={2}
            />
            <Line
              type="monotone"
              dataKey="dapper"
              stroke="#10B981"
              name="Dapper"
              strokeWidth={2}
            />
            <Line
              type="monotone"
              dataKey="efcore"
              stroke="#F59E0B"
              name="EF Core"
              strokeWidth={2}
            />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}

// Helper functions
function calculateCategorySummaries(data: BenchmarkResult[]) {
  const categories = ['A', 'B', 'C', 'D', 'E'];

  return categories.map(category => {
    const categoryData = data.filter(d => d.category === category);

    const nuvatis = avg(categoryData.filter(d => d.orm === 'NuVatis').map(d => d.meanMs));
    const dapper = avg(categoryData.filter(d => d.orm === 'Dapper').map(d => d.meanMs));
    const efcore = avg(categoryData.filter(d => d.orm === 'EfCore').map(d => d.meanMs));

    return {
      category: `Cat ${category}`,
      nuvatis,
      dapper,
      efcore
    };
  });
}

function calculateOrmMetrics(data: BenchmarkResult[]) {
  const orms = ['NuVatis', 'Dapper', 'EfCore'];

  return orms.map(orm => {
    const ormData = data.filter(d => d.orm === orm);

    return {
      orm,
      avgLatency: avg(ormData.map(d => d.meanMs)),
      avgThroughput: avg(ormData.map(d => d.throughput)),
      avgMemory: avg(ormData.map(d => d.memoryMB)),
      totalGC: sum(ormData.map(d => d.gen0 + d.gen1 + d.gen2))
    };
  });
}

function calculateWinnerCounts(data: BenchmarkResult[]) {
  const scenarios = Array.from(new Set(data.map(d => d.scenarioId)));

  const winners: Record<string, number> = { nuvatis: 0, dapper: 0, efcore: 0 };

  scenarios.forEach(scenarioId => {
    const scenarioData = data.filter(d => d.scenarioId === scenarioId);
    const sorted = scenarioData.sort((a, b) => a.meanMs - b.meanMs);
    const winner = sorted[0]?.orm.toLowerCase();
    if (winner) winners[winner]++;
  });

  return winners;
}

function getScenarioTrend(data: BenchmarkResult[]) {
  const scenarios = Array.from(new Set(data.map(d => d.scenarioId))).slice(0, 20);

  return scenarios.map(scenarioId => {
    const scenarioData = data.filter(d => d.scenarioId === scenarioId);

    return {
      scenario: scenarioId,
      nuvatis: scenarioData.find(d => d.orm === 'NuVatis')?.meanMs || 0,
      dapper: scenarioData.find(d => d.orm === 'Dapper')?.meanMs || 0,
      efcore: scenarioData.find(d => d.orm === 'EfCore')?.meanMs || 0
    };
  });
}

function getOrmColor(orm: string) {
  const colors: Record<string, string> = {
    NuVatis: 'bg-indigo-500',
    Dapper: 'bg-green-500',
    EfCore: 'bg-amber-500'
  };
  return colors[orm] || 'bg-gray-500';
}

function getOrmBorderColor(orm: string) {
  const colors: Record<string, string> = {
    NuVatis: 'border-indigo-500',
    Dapper: 'border-green-500',
    EfCore: 'border-amber-500'
  };
  return colors[orm] || 'border-gray-500';
}

function avg(arr: number[]) {
  return arr.length > 0 ? arr.reduce((a, b) => a + b, 0) / arr.length : 0;
}

function sum(arr: number[]) {
  return arr.reduce((a, b) => a + b, 0);
}
