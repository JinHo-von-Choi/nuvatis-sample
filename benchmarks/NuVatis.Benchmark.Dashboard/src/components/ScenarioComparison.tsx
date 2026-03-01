import { BenchmarkResult } from '../types';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';

interface Props {
  data: BenchmarkResult[];
}

export default function ScenarioComparison({ data }: Props) {
  const scenarios = Array.from(new Set(data.map(d => d.scenarioId))).slice(0, 20);

  const chartData = scenarios.map(scenarioId => {
    const scenarioData = data.filter(d => d.scenarioId === scenarioId);

    return {
      scenario: scenarioId,
      NuVatis_Mean: scenarioData.find(d => d.orm === 'NuVatis')?.meanMs || 0,
      Dapper_Mean: scenarioData.find(d => d.orm === 'Dapper')?.meanMs || 0,
      EfCore_Mean: scenarioData.find(d => d.orm === 'EfCore')?.meanMs || 0,
      NuVatis_P95: scenarioData.find(d => d.orm === 'NuVatis')?.p95Ms || 0,
      Dapper_P95: scenarioData.find(d => d.orm === 'Dapper')?.p95Ms || 0,
      EfCore_P95: scenarioData.find(d => d.orm === 'EfCore')?.p95Ms || 0
    };
  });

  return (
    <div className="space-y-6">
      <div className="bg-slate-800 p-6 rounded-xl">
        <h2 className="text-2xl font-bold mb-6">Mean Latency 비교 (상위 20개 시나리오)</h2>
        <ResponsiveContainer width="100%" height={400}>
          <BarChart data={chartData}>
            <CartesianGrid strokeDasharray="3 3" stroke="#475569" />
            <XAxis dataKey="scenario" stroke="#94a3b8" angle={-45} textAnchor="end" height={100} />
            <YAxis stroke="#94a3b8" label={{ value: 'ms', angle: -90, position: 'insideLeft' }} />
            <Tooltip contentStyle={{ backgroundColor: '#1e293b', border: '1px solid #475569' }} />
            <Legend />
            <Bar dataKey="NuVatis_Mean" fill="#4F46E5" name="NuVatis" />
            <Bar dataKey="Dapper_Mean" fill="#10B981" name="Dapper" />
            <Bar dataKey="EfCore_Mean" fill="#F59E0B" name="EF Core" />
          </BarChart>
        </ResponsiveContainer>
      </div>

      <div className="bg-slate-800 p-6 rounded-xl">
        <h2 className="text-2xl font-bold mb-6">P95 Latency 비교 (상위 20개 시나리오)</h2>
        <ResponsiveContainer width="100%" height={400}>
          <BarChart data={chartData}>
            <CartesianGrid strokeDasharray="3 3" stroke="#475569" />
            <XAxis dataKey="scenario" stroke="#94a3b8" angle={-45} textAnchor="end" height={100} />
            <YAxis stroke="#94a3b8" label={{ value: 'ms', angle: -90, position: 'insideLeft' }} />
            <Tooltip contentStyle={{ backgroundColor: '#1e293b', border: '1px solid #475569' }} />
            <Legend />
            <Bar dataKey="NuVatis_P95" fill="#4F46E5" name="NuVatis P95" />
            <Bar dataKey="Dapper_P95" fill="#10B981" name="Dapper P95" />
            <Bar dataKey="EfCore_P95" fill="#F59E0B" name="EF Core P95" />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}
