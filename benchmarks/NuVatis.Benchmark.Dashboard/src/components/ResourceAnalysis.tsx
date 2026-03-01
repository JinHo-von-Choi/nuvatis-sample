import { BenchmarkResult } from '../types';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';

interface Props {
  data: BenchmarkResult[];
}

export default function ResourceAnalysis({ data }: Props) {
  const orms = ['NuVatis', 'Dapper', 'EfCore'];

  const memoryData = orms.map(orm => {
    const ormData = data.filter(d => d.orm === orm);
    return {
      orm,
      avgMemory: avg(ormData.map(d => d.memoryMB)),
      maxMemory: Math.max(...ormData.map(d => d.memoryMB))
    };
  });

  const gcData = orms.map(orm => {
    const ormData = data.filter(d => d.orm === orm);
    return {
      orm,
      Gen0: sum(ormData.map(d => d.gen0)),
      Gen1: sum(ormData.map(d => d.gen1)),
      Gen2: sum(ormData.map(d => d.gen2))
    };
  });

  return (
    <div className="space-y-6">
      <div className="bg-slate-800 p-6 rounded-xl">
        <h2 className="text-2xl font-bold mb-6">메모리 사용량 비교</h2>
        <ResponsiveContainer width="100%" height={300}>
          <BarChart data={memoryData}>
            <CartesianGrid strokeDasharray="3 3" stroke="#475569" />
            <XAxis dataKey="orm" stroke="#94a3b8" />
            <YAxis stroke="#94a3b8" label={{ value: 'MB', angle: -90, position: 'insideLeft' }} />
            <Tooltip contentStyle={{ backgroundColor: '#1e293b', border: '1px solid #475569' }} />
            <Legend />
            <Bar dataKey="avgMemory" fill="#4F46E5" name="평균 메모리" />
            <Bar dataKey="maxMemory" fill="#F59E0B" name="최대 메모리" />
          </BarChart>
        </ResponsiveContainer>
      </div>

      <div className="bg-slate-800 p-6 rounded-xl">
        <h2 className="text-2xl font-bold mb-6">GC 압박 분석</h2>
        <ResponsiveContainer width="100%" height={300}>
          <BarChart data={gcData}>
            <CartesianGrid strokeDasharray="3 3" stroke="#475569" />
            <XAxis dataKey="orm" stroke="#94a3b8" />
            <YAxis stroke="#94a3b8" label={{ value: 'Collections', angle: -90, position: 'insideLeft' }} />
            <Tooltip contentStyle={{ backgroundColor: '#1e293b', border: '1px solid #475569' }} />
            <Legend />
            <Bar dataKey="Gen0" stackId="a" fill="#10B981" />
            <Bar dataKey="Gen1" stackId="a" fill="#F59E0B" />
            <Bar dataKey="Gen2" stackId="a" fill="#EF4444" />
          </BarChart>
        </ResponsiveContainer>
      </div>

      <div className="grid grid-cols-3 gap-6">
        {orms.map(orm => {
          const ormData = data.filter(d => d.orm === orm);
          return (
            <div key={orm} className="bg-slate-800 p-6 rounded-xl">
              <h3 className="text-lg font-bold mb-4">{orm} 리소스 프로파일</h3>
              <div className="space-y-3 text-sm">
                <div>
                  <p className="text-slate-400">평균 메모리</p>
                  <p className="text-xl font-bold">{avg(ormData.map(d => d.memoryMB)).toFixed(1)} MB</p>
                </div>
                <div>
                  <p className="text-slate-400">최대 메모리</p>
                  <p className="text-lg font-semibold">{Math.max(...ormData.map(d => d.memoryMB)).toFixed(1)} MB</p>
                </div>
                <div>
                  <p className="text-slate-400">총 GC (Gen0/1/2)</p>
                  <p className="text-lg font-semibold">
                    {sum(ormData.map(d => d.gen0))} / {sum(ormData.map(d => d.gen1))} / {sum(ormData.map(d => d.gen2))}
                  </p>
                </div>
              </div>
            </div>
          );
        })}
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
