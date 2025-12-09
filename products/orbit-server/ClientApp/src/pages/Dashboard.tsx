import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { Server, PlayCircle, CheckCircle, XCircle, Clock, Activity } from 'lucide-react'
import { getAgents, getJobs } from '@/api/client'
import type { Agent, Job } from '@/types'

function StatCard({
  title,
  value,
  icon: Icon,
  color
}: {
  title: string
  value: number | string
  icon: React.ComponentType<{ className?: string }>
  color: string
}) {
  return (
    <div className="card">
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm text-slate-500 dark:text-slate-400">{title}</p>
          <p className="text-2xl font-bold text-slate-900 dark:text-white mt-1">{value}</p>
        </div>
        <div className={`p-3 rounded-lg ${color}`}>
          <Icon className="w-6 h-6 text-white" />
        </div>
      </div>
    </div>
  )
}

function AgentStatusBadge({ status }: { status: Agent['status'] }) {
  const statusStyles: Record<string, string> = {
    Ready: 'status-ready',
    Running: 'status-busy',
    Disconnected: 'status-disconnected',
    Created: 'status-pending',
    Initializing: 'status-pending',
    Paused: 'bg-blue-500/20 text-blue-600 dark:text-blue-400',
    Stopping: 'bg-orange-500/20 text-orange-600 dark:text-orange-400',
    Stopped: 'bg-slate-500/20 text-slate-600 dark:text-slate-400',
    Faulted: 'bg-red-500/20 text-red-600 dark:text-red-400',
  }

  return (
    <span className={`status-badge ${statusStyles[status] || 'status-pending'}`}>
      {status}
    </span>
  )
}

function JobStatusBadge({ status }: { status: Job['status'] }) {
  const statusStyles: Record<string, string> = {
    Pending: 'bg-slate-500/20 text-slate-600 dark:text-slate-400',
    Assigned: 'bg-blue-500/20 text-blue-600 dark:text-blue-400',
    Running: 'bg-yellow-500/20 text-yellow-600 dark:text-yellow-400',
    Completed: 'bg-green-500/20 text-green-600 dark:text-green-400',
    Failed: 'bg-red-500/20 text-red-600 dark:text-red-400',
    Cancelled: 'bg-slate-500/20 text-slate-600 dark:text-slate-400',
    TimedOut: 'bg-orange-500/20 text-orange-600 dark:text-orange-400',
  }

  return (
    <span className={`status-badge ${statusStyles[status] || 'status-pending'}`}>
      {status}
    </span>
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
    ready: agents.filter(a => a.status === 'Ready').length,
    busy: agents.filter(a => a.status === 'Running').length,
    disconnected: agents.filter(a => a.status === 'Disconnected').length,
  }

  const jobStats = {
    pending: jobs.filter(j => j.status === 'Pending').length,
    running: jobs.filter(j => ['Running', 'Assigned'].includes(j.status)).length,
    completed: jobs.filter(j => j.status === 'Completed').length,
    failed: jobs.filter(j => j.status === 'Failed').length,
  }

  const recentJobs = jobs.slice(0, 5)
  const recentAgents = agents.slice(0, 5)

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-slate-900 dark:text-white">Dashboard</h1>
        <p className="text-slate-500 dark:text-slate-400 mt-1">Overview of your OrbitMesh cluster</p>
      </div>

      {/* Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard
          title="Total Agents"
          value={agentStats.total}
          icon={Server}
          color="bg-orbit-600"
        />
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
          title="Pending Jobs"
          value={jobStats.pending}
          icon={Clock}
          color="bg-blue-600"
        />
      </div>

      {/* Two Column Layout */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Recent Agents */}
        <div className="card">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Recent Agents</h2>
            <Link to="/agents" className="text-sm text-orbit-600 dark:text-orbit-400 hover:text-orbit-500 dark:hover:text-orbit-300">
              View all →
            </Link>
          </div>
          {recentAgents.length === 0 ? (
            <p className="text-slate-500 text-center py-8">No agents connected</p>
          ) : (
            <div className="space-y-3">
              {recentAgents.map((agent) => (
                <div
                  key={agent.id}
                  className="flex items-center justify-between p-3 bg-slate-50 dark:bg-slate-900 rounded-lg"
                >
                  <div className="flex items-center gap-3">
                    <Server className="w-5 h-5 text-slate-400" />
                    <div>
                      <p className="text-slate-900 dark:text-white font-medium">{agent.name}</p>
                      <p className="text-xs text-slate-500 dark:text-slate-500">
                        {agent.capabilities.length} capabilities
                      </p>
                    </div>
                  </div>
                  <AgentStatusBadge status={agent.status} />
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Recent Jobs */}
        <div className="card">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Recent Jobs</h2>
            <Link to="/jobs" className="text-sm text-orbit-600 dark:text-orbit-400 hover:text-orbit-500 dark:hover:text-orbit-300">
              View all →
            </Link>
          </div>
          {recentJobs.length === 0 ? (
            <p className="text-slate-500 text-center py-8">No jobs submitted</p>
          ) : (
            <div className="space-y-3">
              {recentJobs.map((job) => (
                <div
                  key={job.id}
                  className="flex items-center justify-between p-3 bg-slate-50 dark:bg-slate-900 rounded-lg"
                >
                  <div className="flex items-center gap-3">
                    <PlayCircle className="w-5 h-5 text-slate-400" />
                    <div>
                      <p className="text-slate-900 dark:text-white font-medium">{job.command}</p>
                      <p className="text-xs text-slate-500 dark:text-slate-500">
                        {new Date(job.createdAt).toLocaleString()}
                      </p>
                    </div>
                  </div>
                  <JobStatusBadge status={job.status} />
                </div>
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
