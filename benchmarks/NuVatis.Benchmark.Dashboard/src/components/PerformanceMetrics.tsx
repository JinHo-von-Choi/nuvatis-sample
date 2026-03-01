import { BenchmarkResult } from '../types';
import { performanceMetrics } from '../data/scenarioInfo';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';

interface Props {
  data: BenchmarkResult[];
}

export default function PerformanceMetrics({ data }: Props) {
  const orms = ['NuVatis', 'Dapper', 'EfCore'];

  // 메트릭별 집계
  const metricsData = performanceMetrics.slice(0, 6).map(metric => {
    const result: any = { metric: metric.name };

    orms.forEach(orm => {
      const ormData = data.filter(d => d.orm === orm);

      switch (metric.id) {
        case 'latency':
          result[orm] = avg(ormData.map(d => d.meanMs));
          break;
        case 'throughput':
          result[orm] = avg(ormData.map(d => d.throughput));
          break;
        case 'memory':
          result[orm] = avg(ormData.map(d => d.memoryMB));
          break;
        case 'gc':
          result[orm] = sum(ormData.map(d => d.gen0 + d.gen1 + d.gen2));
          break;
        case 'allocatedPerOp':
          result[orm] = avg(ormData.map(d => d.allocatedBytesPerOp)) / 1024; // KB
          break;
        case 'consistency':
          result[orm] = avg(ormData.map(d => d.consistency));
          break;
      }
    });

    return result;
  });

  return (
    <div className="space-y-8">
      {/* 메트릭 설명 카드 */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        {performanceMetrics.slice(0, 4).map(metric => (
          <div
            key={metric.id}
            className="bg-slate-800 p-4 rounded-lg border-l-4"
            style={{
              borderLeftColor:
                metric.importance === 'critical'
                  ? '#EF4444'
                  : metric.importance === 'high'
                  ? '#F59E0B'
                  : '#3B82F6'
            }}
          >
            <div className="flex items-center gap-2 mb-2">
              <span className="text-2xl">{metric.icon}</span>
              <h3 className="font-bold text-sm">{metric.name}</h3>
            </div>
            <p className="text-xs text-slate-400 mb-2">{metric.description}</p>
            <div className="text-xs bg-slate-700 p-2 rounded">
              <span className="text-slate-300">💡 {metric.why}</span>
            </div>
          </div>
        ))}
      </div>

      {/* 메트릭 비교 차트 */}
      <div className="bg-slate-800 p-6 rounded-xl">
        <h2 className="text-2xl font-bold mb-6">성능 메트릭 종합 비교</h2>
        <ResponsiveContainer width="100%" height={400}>
          <BarChart data={metricsData}>
            <CartesianGrid strokeDasharray="3 3" stroke="#475569" />
            <XAxis dataKey="metric" stroke="#94a3b8" angle={-15} textAnchor="end" height={100} />
            <YAxis stroke="#94a3b8" />
            <Tooltip
              contentStyle={{ backgroundColor: '#1e293b', border: '1px solid #475569' }}
            />
            <Legend />
            <Bar dataKey="NuVatis" fill="#4F46E5" />
            <Bar dataKey="Dapper" fill="#10B981" />
            <Bar dataKey="EfCore" fill="#F59E0B" />
          </BarChart>
        </ResponsiveContainer>
      </div>

      {/* 효율성 점수 */}
      <div className="bg-slate-800 p-6 rounded-xl">
        <h2 className="text-2xl font-bold mb-6">효율성 점수 (Efficiency Score)</h2>
        <div className="grid grid-cols-3 gap-6">
          {orms.map(orm => {
            const ormData = data.filter(d => d.orm === orm);

            // 효율성 점수 계산 (낮을수록 좋은 메트릭들의 정규화된 합)
            const avgLatency = avg(ormData.map(d => d.meanMs));
            const avgMemory = avg(ormData.map(d => d.memoryMB));
            const avgAlloc = avg(ormData.map(d => d.allocatedBytesPerOp));
            const totalGC = sum(ormData.map(d => d.gen0 + d.gen1 + d.gen2));

            // 정규화 (0-100 스케일, 낮을수록 좋음)
            const score =
              (avgLatency / 50) * 25 +
              (avgMemory / 50) * 25 +
              (avgAlloc / 100000) * 25 +
              (totalGC / 500) * 25;

            const grade = score < 40 ? 'A' : score < 60 ? 'B' : score < 80 ? 'C' : 'D';
            const gradeColor =
              grade === 'A' ? 'text-green-400' :
              grade === 'B' ? 'text-blue-400' :
              grade === 'C' ? 'text-amber-400' : 'text-red-400';

            return (
              <div key={orm} className="bg-slate-700 p-6 rounded-lg text-center">
                <h3 className="text-xl font-bold mb-4">{orm}</h3>
                <div className={`text-6xl font-bold mb-2 ${gradeColor}`}>{grade}</div>
                <div className="text-2xl font-semibold text-slate-300 mb-4">
                  {(100 - score).toFixed(1)}/100
                </div>
                <div className="space-y-2 text-sm text-left">
                  <div className="flex justify-between">
                    <span className="text-slate-400">응답 시간</span>
                    <span>{avgLatency.toFixed(2)} ms</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-slate-400">메모리</span>
                    <span>{avgMemory.toFixed(1)} MB</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-slate-400">할당/Op</span>
                    <span>{(avgAlloc / 1024).toFixed(1)} KB</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-slate-400">총 GC</span>
                    <span>{totalGC}</span>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* 추가 메트릭 상세 */}
      <div className="grid grid-cols-2 gap-6">
        <div className="bg-slate-800 p-6 rounded-xl">
          <h3 className="text-xl font-bold mb-4">⚙️ CPU 효율성</h3>
          <div className="space-y-3">
            {orms.map(orm => {
              const ormData = data.filter(d => d.orm === orm);
              const avgCpu = avg(ormData.map(d => d.cpuTimeMs));
              const avgLatency = avg(ormData.map(d => d.meanMs));
              const cpuRatio = ((avgCpu / avgLatency) * 100).toFixed(1);

              return (
                <div key={orm} className="flex items-center justify-between p-3 bg-slate-700 rounded">
                  <span className="font-semibold">{orm}</span>
                  <div className="text-right">
                    <div className="text-lg font-bold">{avgCpu.toFixed(2)} ms</div>
                    <div className="text-xs text-slate-400">CPU {cpuRatio}%</div>
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        <div className="bg-slate-800 p-6 rounded-xl">
          <h3 className="text-xl font-bold mb-4">📏 일관성 (Consistency)</h3>
          <div className="space-y-3">
            {orms.map(orm => {
              const ormData = data.filter(d => d.orm === orm);
              const avgConsistency = avg(ormData.map(d => d.consistency));
              const rating =
                avgConsistency < 1 ? '매우 안정적' :
                avgConsistency < 3 ? '안정적' :
                avgConsistency < 5 ? '보통' : '불안정';

              return (
                <div key={orm} className="flex items-center justify-between p-3 bg-slate-700 rounded">
                  <span className="font-semibold">{orm}</span>
                  <div className="text-right">
                    <div className="text-lg font-bold">{avgConsistency.toFixed(2)} ms</div>
                    <div className="text-xs text-slate-400">{rating}</div>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </div>
  );
}

function avg(arr: number[]) {
  return arr.length > 0 ? arr.reduce((a, b) => a + b, 0) / arr.length : 0;
}

function sum(arr: number[]) {
  return arr.reduce((a, b) => a + b, 0);
}
