import { useParams, useNavigate, Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import {
  ArrowLeft,
  Server,
  RefreshCw,
  Clock,
  Tag,
  Activity,
  Cpu,
  HardDrive,
} from 'lucide-react'
import { getAgent, getJobs } from '@/api/client'
import { AgentStatusBadge, JobStatusBadge } from '@/components/ui/status-badge'
import { Button } from '@/components/ui/button'

export default function AgentDetail() {
  const { agentId } = useParams<{ agentId: string }>()
  const navigate = useNavigate()

  const { data: agent, isLoading: loadingAgent, error } = useQuery({
    queryKey: ['agent', agentId],
    queryFn: () => getAgent(agentId!),
    enabled: !!agentId,
  })

  const { data: jobs = [] } = useQuery({
    queryKey: ['jobs'],
    queryFn: () => getJobs(),
  })

  const agentJobs = jobs.filter((job) => job.agentId === agentId)

  if (loadingAgent) {
    return (
      <div className="p-6 flex items-center justify-center">
        <RefreshCw className="w-8 h-8 text-slate-400 animate-spin" />
      </div>
    )
  }

  if (error || !agent) {
    return (
      <div className="p-6">
        <div className="text-center py-12">
          <Server className="w-12 h-12 text-slate-400 mx-auto mb-4" />
          <h3 className="text-lg font-medium text-slate-900 dark:text-white mb-2">Agent not found</h3>
          <p className="text-slate-500 dark:text-slate-400 mb-4">
            The requested agent could not be found.
          </p>
          <Button onClick={() => navigate('/agents')}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back to Agents
          </Button>
        </div>
      </div>
    )
  }

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="sm" onClick={() => navigate('/agents')}>
          <ArrowLeft className="w-4 h-4 mr-2" />
          Back
        </Button>
        <div className="h-6 w-px bg-border" />
        <div className="flex items-center gap-3">
          <div className="p-2 bg-orbit-100 dark:bg-orbit-600/20 rounded-lg">
            <Server className="w-6 h-6 text-orbit-600 dark:text-orbit-400" />
          </div>
          <div>
            <h1 className="text-2xl font-bold text-slate-900 dark:text-white">{agent.name}</h1>
            <p className="text-sm text-slate-500 dark:text-slate-400 font-mono">{agent.id}</p>
          </div>
        </div>
        <div className="ml-auto">
          <AgentStatusBadge status={agent.status} />
        </div>
      </div>

      {/* Overview Cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="card">
          <div className="flex items-center gap-3">
            <Activity className="w-5 h-5 text-slate-400" />
            <span className="text-slate-600 dark:text-slate-400">Status</span>
          </div>
          <div className="mt-2">
            <AgentStatusBadge status={agent.status} />
          </div>
        </div>

        <div className="card">
          <div className="flex items-center gap-3">
            <Tag className="w-5 h-5 text-slate-400" />
            <span className="text-slate-600 dark:text-slate-400">Group</span>
          </div>
          <p className="text-lg font-semibold text-slate-900 dark:text-white mt-2">
            {agent.group || 'No group'}
          </p>
        </div>

        <div className="card">
          <div className="flex items-center gap-3">
            <Clock className="w-5 h-5 text-slate-400" />
            <span className="text-slate-600 dark:text-slate-400">Last Heartbeat</span>
          </div>
          <p className="text-lg font-semibold text-slate-900 dark:text-white mt-2">
            {agent.lastHeartbeat
              ? new Date(agent.lastHeartbeat).toLocaleString()
              : 'Never'}
          </p>
        </div>
      </div>

      {/* Capabilities */}
      <div className="card">
        <div className="flex items-center gap-3 mb-4">
          <Cpu className="w-5 h-5 text-orbit-600 dark:text-orbit-400" />
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Capabilities</h2>
          <span className="text-sm text-slate-500 dark:text-slate-400 ml-auto">
            {agent.capabilities.length} registered
          </span>
        </div>

        {agent.capabilities.length === 0 ? (
          <p className="text-slate-500 dark:text-slate-400 text-center py-8">
            No capabilities registered
          </p>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {agent.capabilities.map((cap) => (
              <div
                key={cap.name}
                className="p-4 bg-slate-50 dark:bg-slate-900 rounded-lg border border-slate-200 dark:border-slate-700"
              >
                <div className="flex items-center justify-between mb-2">
                  <h3 className="font-medium text-slate-900 dark:text-white">{cap.name}</h3>
                  {cap.version && (
                    <span className="text-xs text-slate-500 dark:text-slate-400">v{cap.version}</span>
                  )}
                </div>
                {cap.parameters && cap.parameters.length > 0 && (
                  <div className="mt-3 space-y-2">
                    <p className="text-xs text-slate-500 dark:text-slate-400 uppercase">Parameters</p>
                    {cap.parameters.map((param) => (
                      <div key={param.name} className="flex items-center gap-2 text-sm">
                        <span className="text-slate-700 dark:text-slate-300">{param.name}</span>
                        <span className="text-xs text-slate-400">({param.type})</span>
                        {param.isRequired && (
                          <span className="text-xs text-red-500">*</span>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Metadata */}
      {agent.metadata && Object.keys(agent.metadata).length > 0 && (
        <div className="card">
          <div className="flex items-center gap-3 mb-4">
            <HardDrive className="w-5 h-5 text-orbit-600 dark:text-orbit-400" />
            <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Metadata</h2>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {Object.entries(agent.metadata).map(([key, value]) => (
              <div
                key={key}
                className="p-3 bg-slate-50 dark:bg-slate-900 rounded-lg"
              >
                <p className="text-xs text-slate-500 dark:text-slate-400 uppercase">{key}</p>
                <p className="text-slate-900 dark:text-white mt-1 font-mono text-sm break-all">
                  {value}
                </p>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Recent Jobs */}
      <div className="card">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-3">
            <Activity className="w-5 h-5 text-orbit-600 dark:text-orbit-400" />
            <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Recent Jobs</h2>
          </div>
          <Link
            to={`/jobs?agentId=${agentId}`}
            className="text-sm text-orbit-600 dark:text-orbit-400 hover:text-orbit-500"
          >
            View all →
          </Link>
        </div>

        {agentJobs.length === 0 ? (
          <p className="text-slate-500 dark:text-slate-400 text-center py-8">
            No jobs assigned to this agent
          </p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="table-header">
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Job ID</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Command</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Status</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Created</th>
                </tr>
              </thead>
              <tbody>
                {agentJobs.slice(0, 10).map((job) => (
                  <tr key={job.id} className="table-row hover:bg-slate-50 dark:hover:bg-slate-800/50">
                    <td className="py-3 px-4">
                      <Link
                        to={`/jobs/${job.id}`}
                        className="text-xs text-slate-500 dark:text-slate-400 font-mono hover:text-orbit-600"
                      >
                        {job.id.substring(0, 8)}...
                      </Link>
                    </td>
                    <td className="py-3 px-4">
                      <span className="text-slate-900 dark:text-white font-medium">{job.command}</span>
                    </td>
                    <td className="py-3 px-4">
                      <JobStatusBadge status={job.status} />
                    </td>
                    <td className="py-3 px-4">
                      <span className="text-sm text-slate-500 dark:text-slate-400">
                        {new Date(job.createdAt).toLocaleString()}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  )
}
