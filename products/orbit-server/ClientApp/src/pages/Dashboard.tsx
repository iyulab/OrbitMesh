import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { Server, PlayCircle, CheckCircle, XCircle, Clock, Activity, TrendingUp } from 'lucide-react'
import {
  PieChart,
  Pie,
  Cell,
  AreaChart,
  Area,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  Legend,
} from 'recharts'
import { getAgents, getJobs } from '@/api/client'
import { AgentStatusBadge, JobStatusBadge } from '@/components/ui/status-badge'
import type { Job } from '@/types'

function StatCard({
  title,
  value,
  icon: Icon,
  color,
  trend,
}: {
  title: string
  value: number | string
  icon: React.ComponentType<{ className?: string }>
  color: string
  trend?: { value: number; label: string }
}) {
  return (
    <div className="card">
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm text-slate-500 dark:text-slate-400">{title}</p>
          <p className="text-2xl font-bold text-slate-900 dark:text-white mt-1">{value}</p>
          {trend && (
            <p className={`text-xs mt-1 flex items-center gap-1 ${trend.value >= 0 ? 'text-green-600' : 'text-red-600'}`}>
              <TrendingUp className={`w-3 h-3 ${trend.value < 0 ? 'rotate-180' : ''}`} />
              {Math.abs(trend.value)}% {trend.label}
            </p>
          )}
        </div>
        <div className={`p-3 rounded-lg ${color}`}>
          <Icon className="w-6 h-6 text-white" />
        </div>
      </div>
    </div>
  )
}

const JOB_STATUS_COLORS: Record<string, string> = {
  Pending: '#3b82f6',
  Assigned: '#8b5cf6',
  Running: '#eab308',
  Completed: '#22c55e',
  Failed: '#ef4444',
  Cancelled: '#6b7280',
  TimedOut: '#f97316',
}

const AGENT_STATUS_COLORS: Record<string, string> = {
  Ready: '#22c55e',
  Running: '#eab308',
  Disconnected: '#ef4444',
  Unknown: '#6b7280',
}

function JobStatusChart({ jobs }: { jobs: Job[] }) {
  const data = useMemo(() => {
    const statusCounts: Record<string, number> = {}
    jobs.forEach((job) => {
      statusCounts[job.status] = (statusCounts[job.status] || 0) + 1
    })
    return Object.entries(statusCounts).map(([status, count]) => ({
      name: status,
      value: count,
      color: JOB_STATUS_COLORS[status] || '#6b7280',
    }))
  }, [jobs])

  if (data.length === 0) {
    return (
      <div className="h-[200px] flex items-center justify-center text-slate-500">
        No job data available
      </div>
    )
  }

  return (
    <ResponsiveContainer width="100%" height={200}>
      <PieChart>
        <Pie
          data={data}
          cx="50%"
          cy="50%"
          innerRadius={50}
          outerRadius={80}
          paddingAngle={2}
          dataKey="value"
        >
          {data.map((entry, index) => (
            <Cell key={`cell-${index}`} fill={entry.color} />
          ))}
        </Pie>
        <Tooltip
          contentStyle={{
            backgroundColor: 'var(--tooltip-bg, #1f2937)',
            border: 'none',
            borderRadius: '8px',
            color: 'var(--tooltip-text, #f9fafb)',
          }}
        />
        <Legend
          verticalAlign="middle"
          align="right"
          layout="vertical"
          wrapperStyle={{ paddingLeft: '20px' }}
        />
      </PieChart>
    </ResponsiveContainer>
  )
}

function JobActivityChart({ jobs }: { jobs: Job[] }) {
  const data = useMemo(() => {
    const now = new Date()
    const hourlyData: Record<string, { hour: string; completed: number; failed: number; submitted: number }> = {}

    // Initialize last 12 hours
    for (let i = 11; i >= 0; i--) {
      const hour = new Date(now.getTime() - i * 60 * 60 * 1000)
      const hourKey = hour.toLocaleTimeString('en-US', { hour: '2-digit', hour12: true })
      hourlyData[hourKey] = { hour: hourKey, completed: 0, failed: 0, submitted: 0 }
    }

    // Count jobs by hour
    jobs.forEach((job) => {
      const jobDate = new Date(job.createdAt)
      const hoursDiff = (now.getTime() - jobDate.getTime()) / (1000 * 60 * 60)

      if (hoursDiff <= 12) {
        const hourKey = jobDate.toLocaleTimeString('en-US', { hour: '2-digit', hour12: true })
        if (hourlyData[hourKey]) {
          hourlyData[hourKey].submitted++
          if (job.status === 'Completed') {
            hourlyData[hourKey].completed++
          } else if (job.status === 'Failed') {
            hourlyData[hourKey].failed++
          }
        }
      }
    })

    return Object.values(hourlyData)
  }, [jobs])

  return (
    <ResponsiveContainer width="100%" height={200}>
      <AreaChart data={data}>
        <defs>
          <linearGradient id="colorCompleted" x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%" stopColor="#22c55e" stopOpacity={0.3} />
            <stop offset="95%" stopColor="#22c55e" stopOpacity={0} />
          </linearGradient>
          <linearGradient id="colorFailed" x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%" stopColor="#ef4444" stopOpacity={0.3} />
            <stop offset="95%" stopColor="#ef4444" stopOpacity={0} />
          </linearGradient>
          <linearGradient id="colorSubmitted" x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%" stopColor="#3b82f6" stopOpacity={0.3} />
            <stop offset="95%" stopColor="#3b82f6" stopOpacity={0} />
          </linearGradient>
        </defs>
        <XAxis
          dataKey="hour"
          tick={{ fontSize: 10, fill: '#94a3b8' }}
          axisLine={{ stroke: '#334155' }}
          tickLine={false}
        />
        <YAxis
          tick={{ fontSize: 10, fill: '#94a3b8' }}
          axisLine={{ stroke: '#334155' }}
          tickLine={false}
          allowDecimals={false}
        />
        <Tooltip
          contentStyle={{
            backgroundColor: '#1f2937',
            border: 'none',
            borderRadius: '8px',
            color: '#f9fafb',
          }}
        />
        <Area
          type="monotone"
          dataKey="submitted"
          stroke="#3b82f6"
          fillOpacity={1}
          fill="url(#colorSubmitted)"
          name="Submitted"
        />
        <Area
          type="monotone"
          dataKey="completed"
          stroke="#22c55e"
          fillOpacity={1}
          fill="url(#colorCompleted)"
          name="Completed"
        />
        <Area
          type="monotone"
          dataKey="failed"
          stroke="#ef4444"
          fillOpacity={1}
          fill="url(#colorFailed)"
          name="Failed"
        />
        <Legend wrapperStyle={{ paddingTop: '10px' }} />
      </AreaChart>
    </ResponsiveContainer>
  )
}

function AgentStatusChart({ agents }: { agents: Array<{ status: string }> }) {
  const data = useMemo(() => {
    const statusCounts: Record<string, number> = {}
    agents.forEach((agent) => {
      statusCounts[agent.status] = (statusCounts[agent.status] || 0) + 1
    })
    return Object.entries(statusCounts).map(([status, count]) => ({
      name: status,
      value: count,
      color: AGENT_STATUS_COLORS[status] || '#6b7280',
    }))
  }, [agents])

  if (data.length === 0) {
    return (
      <div className="h-[120px] flex items-center justify-center text-slate-500">
        No agents connected
      </div>
    )
  }

  return (
    <ResponsiveContainer width="100%" height={120}>
      <PieChart>
        <Pie
          data={data}
          cx="50%"
          cy="50%"
          innerRadius={30}
          outerRadius={50}
          paddingAngle={2}
          dataKey="value"
        >
          {data.map((entry, index) => (
            <Cell key={`cell-${index}`} fill={entry.color} />
          ))}
        </Pie>
        <Tooltip
          contentStyle={{
            backgroundColor: '#1f2937',
            border: 'none',
            borderRadius: '8px',
            color: '#f9fafb',
          }}
        />
        <Legend
          verticalAlign="middle"
          align="right"
          layout="vertical"
          wrapperStyle={{ paddingLeft: '10px', fontSize: '12px' }}
        />
      </PieChart>
    </ResponsiveContainer>
  )
}

export default function Dashboard() {
  const { data: agents = [] } = useQuery({
    queryKey: ['agents'],
    queryFn: getAgents,
  })

  const { data: jobs = [] } = useQuery({
    queryKey: ['jobs'],
    queryFn: () => getJobs(),
  })

  const agentStats = {
    total: agents.length,
    ready: agents.filter((a) => a.status === 'Ready').length,
    busy: agents.filter((a) => a.status === 'Running').length,
    disconnected: agents.filter((a) => a.status === 'Disconnected').length,
  }

  const jobStats = {
    pending: jobs.filter((j) => j.status === 'Pending').length,
    running: jobs.filter((j) => ['Running', 'Assigned'].includes(j.status)).length,
    completed: jobs.filter((j) => j.status === 'Completed').length,
    failed: jobs.filter((j) => j.status === 'Failed').length,
  }

  const recentJobs = jobs.slice(0, 5)
  const recentAgents = agents.slice(0, 5)

  // Calculate success rate
  const totalFinished = jobStats.completed + jobStats.failed
  const successRate = totalFinished > 0 ? Math.round((jobStats.completed / totalFinished) * 100) : 100

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-slate-900 dark:text-white">Dashboard</h1>
        <p className="text-slate-500 dark:text-slate-400 mt-1">Overview of your OrbitMesh cluster</p>
      </div>

      {/* Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard title="Total Agents" value={agentStats.total} icon={Server} color="bg-orbit-600" />
        <StatCard
          title="Ready Agents"
          value={agentStats.ready}
          icon={CheckCircle}
          color="bg-green-600"
        />
        <StatCard
          title="Running Jobs"
          value={jobStats.running}
          icon={Activity}
          color="bg-yellow-600"
        />
        <StatCard
          title="Success Rate"
          value={`${successRate}%`}
          icon={TrendingUp}
          color="bg-blue-600"
        />
      </div>

      {/* Charts Row */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Job Status Distribution */}
        <div className="card">
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white mb-4">
            Job Status Distribution
          </h2>
          <JobStatusChart jobs={jobs} />
        </div>

        {/* Job Activity Timeline */}
        <div className="card">
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white mb-4">
            Job Activity (Last 12 Hours)
          </h2>
          <JobActivityChart jobs={jobs} />
        </div>
      </div>

      {/* Three Column Layout */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Agent Status */}
        <div className="card">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Agent Status</h2>
            <Link
              to="/agents"
              className="text-sm text-orbit-600 dark:text-orbit-400 hover:text-orbit-500"
            >
              View all →
            </Link>
          </div>
          <AgentStatusChart agents={agents} />
          <div className="mt-4 grid grid-cols-3 gap-2 text-center">
            <div className="p-2 bg-green-50 dark:bg-green-900/20 rounded-lg">
              <p className="text-xl font-bold text-green-600 dark:text-green-400">
                {agentStats.ready}
              </p>
              <p className="text-xs text-slate-500">Ready</p>
            </div>
            <div className="p-2 bg-yellow-50 dark:bg-yellow-900/20 rounded-lg">
              <p className="text-xl font-bold text-yellow-600 dark:text-yellow-400">
                {agentStats.busy}
              </p>
              <p className="text-xs text-slate-500">Busy</p>
            </div>
            <div className="p-2 bg-red-50 dark:bg-red-900/20 rounded-lg">
              <p className="text-xl font-bold text-red-600 dark:text-red-400">
                {agentStats.disconnected}
              </p>
              <p className="text-xs text-slate-500">Offline</p>
            </div>
          </div>
        </div>

        {/* Recent Agents */}
        <div className="card">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Recent Agents</h2>
            <Link
              to="/agents"
              className="text-sm text-orbit-600 dark:text-orbit-400 hover:text-orbit-500"
            >
              View all →
            </Link>
          </div>
          {recentAgents.length === 0 ? (
            <p className="text-slate-500 text-center py-8">No agents connected</p>
          ) : (
            <div className="space-y-2">
              {recentAgents.map((agent) => (
                <Link
                  key={agent.id}
                  to={`/agents/${agent.id}`}
                  className="flex items-center justify-between p-2 bg-slate-50 dark:bg-slate-900 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
                >
                  <div className="flex items-center gap-2">
                    <Server className="w-4 h-4 text-slate-400" />
                    <div>
                      <p className="text-sm text-slate-900 dark:text-white font-medium">
                        {agent.name}
                      </p>
                      <p className="text-xs text-slate-500">{agent.capabilities.length} caps</p>
                    </div>
                  </div>
                  <AgentStatusBadge status={agent.status} />
                </Link>
              ))}
            </div>
          )}
        </div>

        {/* Recent Jobs */}
        <div className="card">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Recent Jobs</h2>
            <Link
              to="/jobs"
              className="text-sm text-orbit-600 dark:text-orbit-400 hover:text-orbit-500"
            >
              View all →
            </Link>
          </div>
          {recentJobs.length === 0 ? (
            <p className="text-slate-500 text-center py-8">No jobs submitted</p>
          ) : (
            <div className="space-y-2">
              {recentJobs.map((job) => (
                <Link
                  key={job.id}
                  to={`/jobs/${job.id}`}
                  className="flex items-center justify-between p-2 bg-slate-50 dark:bg-slate-900 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
                >
                  <div className="flex items-center gap-2">
                    <PlayCircle className="w-4 h-4 text-slate-400" />
                    <div>
                      <p className="text-sm text-slate-900 dark:text-white font-medium truncate max-w-[120px]">
                        {job.command}
                      </p>
                      <p className="text-xs text-slate-500">
                        {new Date(job.createdAt).toLocaleTimeString()}
                      </p>
                    </div>
                  </div>
                  <JobStatusBadge status={job.status} />
                </Link>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Job Statistics */}
      <div className="card">
        <h2 className="text-lg font-semibold text-slate-900 dark:text-white mb-4">Job Statistics</h2>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div className="text-center p-4 bg-slate-50 dark:bg-slate-900 rounded-lg">
            <Clock className="w-8 h-8 text-blue-500 dark:text-blue-400 mx-auto mb-2" />
            <p className="text-2xl font-bold text-slate-900 dark:text-white">{jobStats.pending}</p>
            <p className="text-sm text-slate-500 dark:text-slate-400">Pending</p>
          </div>
          <div className="text-center p-4 bg-slate-50 dark:bg-slate-900 rounded-lg">
            <Activity className="w-8 h-8 text-yellow-500 dark:text-yellow-400 mx-auto mb-2" />
            <p className="text-2xl font-bold text-slate-900 dark:text-white">{jobStats.running}</p>
            <p className="text-sm text-slate-500 dark:text-slate-400">Running</p>
          </div>
          <div className="text-center p-4 bg-slate-50 dark:bg-slate-900 rounded-lg">
            <CheckCircle className="w-8 h-8 text-green-500 dark:text-green-400 mx-auto mb-2" />
            <p className="text-2xl font-bold text-slate-900 dark:text-white">{jobStats.completed}</p>
            <p className="text-sm text-slate-500 dark:text-slate-400">Completed</p>
          </div>
          <div className="text-center p-4 bg-slate-50 dark:bg-slate-900 rounded-lg">
            <XCircle className="w-8 h-8 text-red-500 dark:text-red-400 mx-auto mb-2" />
            <p className="text-2xl font-bold text-slate-900 dark:text-white">{jobStats.failed}</p>
            <p className="text-sm text-slate-500 dark:text-slate-400">Failed</p>
          </div>
        </div>
      </div>
    </div>
  )
}
