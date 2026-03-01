import { useState } from 'react';
import { BenchmarkResult } from '../types';
import { categoryInfo, scenarioDetails } from '../data/scenarioInfo';

interface Props {
  category: string;
  data: BenchmarkResult[];
  onCategoryChange: (category: string) => void;
}

export default function CategoryDetail({ category, data, onCategoryChange }: Props) {
  const [selectedScenario, setSelectedScenario] = useState<string | null>(null);
  const categoryData = data.filter(d => d.category === category);
  const scenarios = Array.from(new Set(categoryData.map(d => d.scenarioId)));
  const catInfo = categoryInfo[category];
  const selectedScenarioInfo = selectedScenario ? scenarioDetails[selectedScenario] : null;

  return (
    <div className="space-y-6">
      {/* 카테고리 정보 */}
      <div className="bg-gradient-to-r from-slate-800 to-slate-700 p-6 rounded-xl border-l-4 border-indigo-500">
        <div className="flex items-center gap-3 mb-4">
          <span className="text-4xl">{catInfo?.icon}</span>
          <div>
            <h2 className="text-2xl font-bold">{catInfo?.name}</h2>
            <p className="text-slate-300">{catInfo?.description}</p>
          </div>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
          <div>
            <h4 className="text-sm font-semibold text-indigo-400 mb-2">🎯 Focus</h4>
            <p className="text-sm text-slate-300">{catInfo?.focus}</p>
          </div>
          <div>
            <h4 className="text-sm font-semibold text-indigo-400 mb-2">📝 Examples</h4>
            <ul className="text-sm text-slate-300 space-y-1">
              {catInfo?.examples.map((ex, i) => (
                <li key={i}>• {ex}</li>
              ))}
            </ul>
          </div>
        </div>
      </div>

      {/* 카테고리 선택 */}
      <div className="flex gap-2">
        {['A', 'B', 'C', 'D', 'E'].map(cat => {
          const info = categoryInfo[cat];
          return (
            <button
              key={cat}
              onClick={() => onCategoryChange(cat)}
              className={`px-4 py-2 rounded-lg font-medium transition flex items-center gap-2 ${
                category === cat
                  ? 'bg-indigo-600 text-white'
                  : 'bg-slate-700 text-slate-300 hover:bg-slate-600'
              }`}
            >
              <span>{info?.icon}</span>
              <span>Category {cat}</span>
            </button>
          );
        })}
      </div>

      {/* 시나리오 테이블 */}
      <div className="bg-slate-800 rounded-xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead className="bg-slate-700">
              <tr>
                <th className="px-4 py-3 text-left text-sm font-medium">Scenario</th>
                <th className="px-4 py-3 text-left text-sm font-medium">Description</th>
                <th className="px-4 py-3 text-right text-sm font-medium">NuVatis (ms)</th>
                <th className="px-4 py-3 text-right text-sm font-medium">Dapper (ms)</th>
                <th className="px-4 py-3 text-right text-sm font-medium">EF Core (ms)</th>
                <th className="px-4 py-3 text-center text-sm font-medium">Winner</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-700">
              {scenarios.map(scenarioId => {
                const scenario = categoryData.filter(d => d.scenarioId === scenarioId);
                const nuvatis = scenario.find(d => d.orm === 'NuVatis');
                const dapper = scenario.find(d => d.orm === 'Dapper');
                const efcore = scenario.find(d => d.orm === 'EfCore');

                const winner = [nuvatis, dapper, efcore]
                  .filter(Boolean)
                  .sort((a, b) => a!.meanMs - b!.meanMs)[0];

                return (
                  <tr
                    key={scenarioId}
                    className="hover:bg-slate-700/50 transition cursor-pointer"
                    onClick={() => setSelectedScenario(scenarioId)}
                  >
                    <td className="px-4 py-3 font-mono text-sm text-indigo-400">
                      {scenarioId}
                    </td>
                    <td className="px-4 py-3 text-sm">{nuvatis?.description}</td>
                    <td
                      className={`px-4 py-3 text-right font-mono text-sm ${
                        winner?.orm === 'NuVatis' ? 'text-green-400 font-bold' : ''
                      }`}
                    >
                      {nuvatis?.meanMs.toFixed(2)}
                    </td>
                    <td
                      className={`px-4 py-3 text-right font-mono text-sm ${
                        winner?.orm === 'Dapper' ? 'text-green-400 font-bold' : ''
                      }`}
                    >
                      {dapper?.meanMs.toFixed(2)}
                    </td>
                    <td
                      className={`px-4 py-3 text-right font-mono text-sm ${
                        winner?.orm === 'EfCore' ? 'text-green-400 font-bold' : ''
                      }`}
                    >
                      {efcore?.meanMs.toFixed(2)}
                    </td>
                    <td className="px-4 py-3 text-center">
                      <span
                        className={`inline-block px-2 py-1 rounded text-xs font-bold ${
                          winner?.orm === 'NuVatis'
                            ? 'bg-indigo-600'
                            : winner?.orm === 'Dapper'
                            ? 'bg-green-600'
                            : 'bg-amber-600'
                        }`}
                      >
                        {winner?.orm}
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>

      {/* 시나리오 상세 모달 */}
      {selectedScenarioInfo && (
        <div
          className="fixed inset-0 bg-black/70 flex items-center justify-center z-50"
          onClick={() => setSelectedScenario(null)}
        >
          <div
            className="bg-slate-800 rounded-xl p-8 max-w-2xl w-full mx-4 border border-slate-700"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex justify-between items-start mb-6">
              <div>
                <h3 className="text-2xl font-bold text-indigo-400">{selectedScenarioInfo.id}</h3>
                <p className="text-xl text-slate-200 mt-2">{selectedScenarioInfo.name}</p>
              </div>
              <button
                onClick={() => setSelectedScenario(null)}
                className="text-slate-400 hover:text-white text-2xl"
              >
                ×
              </button>
            </div>

            <div className="space-y-4">
              <div>
                <h4 className="text-sm font-semibold text-indigo-400 mb-2">📋 Description</h4>
                <p className="text-slate-300">{selectedScenarioInfo.description}</p>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <h4 className="text-sm font-semibold text-indigo-400 mb-2">⚙️ Complexity</h4>
                  <p className="text-slate-300">{selectedScenarioInfo.complexity}</p>
                </div>
                <div>
                  <h4 className="text-sm font-semibold text-indigo-400 mb-2">🏆 Expected Winner</h4>
                  <p className="text-slate-300">{selectedScenarioInfo.expectedWinner}</p>
                </div>
              </div>

              <div>
                <h4 className="text-sm font-semibold text-indigo-400 mb-2">🌍 Real World Use</h4>
                <p className="text-slate-300">{selectedScenarioInfo.realWorldUse}</p>
              </div>

              <div>
                <h4 className="text-sm font-semibold text-indigo-400 mb-2">💡 Why It Matters</h4>
                <p className="text-slate-300">{selectedScenarioInfo.whyItMatters}</p>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
