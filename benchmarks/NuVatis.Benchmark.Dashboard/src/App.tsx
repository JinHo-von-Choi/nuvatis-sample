import { useState, useEffect } from 'react';
import OverviewDashboard from './components/OverviewDashboard';
import CategoryDetail from './components/CategoryDetail';
import ScenarioComparison from './components/ScenarioComparison';
import ResourceAnalysis from './components/ResourceAnalysis';
import PerformanceMetrics from './components/PerformanceMetrics';
import { mockBenchmarkData } from './data/mockData';
import { loadBenchmarkResults } from './utils/benchmarkParser';
import { BenchmarkResult } from './types';

type View = 'overview' | 'category' | 'scenario' | 'resource' | 'metrics';

function App() {
  const [currentView, setCurrentView] = useState<View>('overview');
  const [selectedCategory, setSelectedCategory] = useState<string>('A');
  const [benchmarkData, setBenchmarkData] = useState<BenchmarkResult[]>(mockBenchmarkData);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    loadBenchmarkResults().then(data => {
      if (data.length > 0) {
        setBenchmarkData(data);
        console.log('✅ 실제 벤치마크 데이터 로딩 완료:', data.length, '개');
      } else {
        console.log('⚠️ Mock 데이터 사용 중');
      }
      setIsLoading(false);
    });
  }, []);

  return (
    <div className="min-h-screen bg-slate-900 text-slate-100">
      {/* Header */}
      <header className="bg-slate-800 border-b border-slate-700 sticky top-0 z-50">
        <div className="container mx-auto px-6 py-4">
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-3xl font-bold bg-gradient-to-r from-indigo-400 to-purple-400 bg-clip-text text-transparent">
                NuVatis ORM Benchmark
              </h1>
              <p className="text-slate-400 text-sm mt-1">
                종합 성능 분석 대시보드 • 60개 시나리오 • 3-Way Comparison
              </p>
            </div>
            <div className="flex gap-2">
              <button className="px-4 py-2 bg-slate-700 hover:bg-slate-600 rounded-lg text-sm transition">
                📥 Export CSV
              </button>
              <button className="px-4 py-2 bg-slate-700 hover:bg-slate-600 rounded-lg text-sm transition">
                📊 Export PNG
              </button>
            </div>
          </div>
        </div>
      </header>

      {/* Navigation */}
      <nav className="bg-slate-800 border-b border-slate-700">
        <div className="container mx-auto px-6">
          <div className="flex gap-1">
            {[
              { id: 'overview', label: '📈 Overview', icon: '📈' },
              { id: 'category', label: '📂 Category Detail', icon: '📂' },
              { id: 'scenario', label: '🔍 Scenario Comparison', icon: '🔍' },
              { id: 'resource', label: '💻 Resource Analysis', icon: '💻' },
              { id: 'metrics', label: '⚡ Performance Metrics', icon: '⚡' }
            ].map(view => (
              <button
                key={view.id}
                onClick={() => setCurrentView(view.id as View)}
                className={`px-6 py-3 text-sm font-medium transition border-b-2 ${
                  currentView === view.id
                    ? 'border-indigo-500 text-indigo-400'
                    : 'border-transparent text-slate-400 hover:text-slate-300'
                }`}
              >
                {view.label}
              </button>
            ))}
          </div>
        </div>
      </nav>

      {/* Main Content */}
      <main className="container mx-auto px-6 py-8">
        {isLoading ? (
          <div className="flex items-center justify-center h-64">
            <div className="text-center">
              <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-indigo-500 mx-auto"></div>
              <p className="mt-4 text-slate-400">벤치마크 데이터 로딩 중...</p>
            </div>
          </div>
        ) : (
          <>
            {currentView === 'overview' && <OverviewDashboard data={benchmarkData} />}
            {currentView === 'category' && (
              <CategoryDetail
                category={selectedCategory}
                data={benchmarkData}
                onCategoryChange={setSelectedCategory}
              />
            )}
            {currentView === 'scenario' && <ScenarioComparison data={benchmarkData} />}
            {currentView === 'resource' && <ResourceAnalysis data={benchmarkData} />}
            {currentView === 'metrics' && <PerformanceMetrics data={benchmarkData} />}
          </>
        )}
      </main>

      {/* Footer */}
      <footer className="bg-slate-800 border-t border-slate-700 mt-16">
        <div className="container mx-auto px-6 py-6">
          <div className="flex items-center justify-between text-sm text-slate-400">
            <div>
              <p>© 2026 NuVatis Benchmark • 작성자: 최진호</p>
              <p className="mt-1">마지막 실행: {new Date().toLocaleString('ko-KR')}</p>
            </div>
            <div className="flex gap-6">
              <div className="flex items-center gap-2">
                <div className="w-3 h-3 bg-indigo-500 rounded-full"></div>
                <span>NuVatis</span>
              </div>
              <div className="flex items-center gap-2">
                <div className="w-3 h-3 bg-green-500 rounded-full"></div>
                <span>Dapper</span>
              </div>
              <div className="flex items-center gap-2">
                <div className="w-3 h-3 bg-amber-500 rounded-full"></div>
                <span>EF Core</span>
              </div>
            </div>
          </div>
        </div>
      </footer>
    </div>
  );
}

export default App;
