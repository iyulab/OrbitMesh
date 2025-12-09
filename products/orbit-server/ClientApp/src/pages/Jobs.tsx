import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  PlayCircle,
  Plus,
  RefreshCw,
  XCircle,
  Clock,
  CheckCircle,
  Activity,
  AlertTriangle,
} from 'lucide-react'
import { getJobs, submitJob, cancelJob } from '@/api/client'
import type { Job, JobStatus } from '@/types'

function JobStatusBadge({ status }: { status: JobStatus }) {
  const config: Record<JobStatus, { class: string; icon: React.ComponentType<{ className?: string }> }> = {
    Pending: { class: 'bg-slate-500/20 text-slate-600 dark:text-slate-400', icon: Clock },
    Assigned: { class: 'bg-blue-500/20 text-blue-600 dark:text-blue-400', icon: Activity },
    Acknowledged: { class: 'bg-blue-500/20 text-blue-600 dark:text-blue-400', icon: Activity },
    Running: { class: 'bg-yellow-500/20 text-yellow-600 dark:text-yellow-400', icon: Activity },
    Completed: { class: 'bg-green-500/20 text-green-600 dark:text-green-400', icon: CheckCircle },
    Failed: { class: 'bg-red-500/20 text-red-600 dark:text-red-400', icon: XCircle },
    Cancelled: { class: 'bg-slate-500/20 text-slate-600 dark:text-slate-400', icon: XCircle },
    TimedOut: { class: 'bg-orange-500/20 text-orange-600 dark:text-orange-400', icon: AlertTriangle },
  }

  const { class: className, icon: Icon } = config[status] || config.Pending

  return (
    <span className={`status-badge flex items-center gap-1 ${className}`}>
      <Icon className="w-3 h-3" />
      {status}
    </span>
  )
}

function SubmitJobModal({
  isOpen,
  onClose,
}: {
  isOpen: boolean
  onClose: () => void
}) {
  const [command, setCommand] = useState('')
  const [pattern, setPattern] = useState('')
  const [priority, setPriority] = useState('5')
  const [payload, setPayload] = useState('')

  const queryClient = useQueryClient()

  const submitMutation = useMutation({
    mutationFn: submitJob,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['jobs'] })
      handleClose()
    },
  })

  const handleSubmit = () => {
    let parsedPayload: object | undefined
    if (payload.trim()) {
      try {
        parsedPayload = JSON.parse(payload)
      } catch {
        alert('Invalid JSON payload')
        return
      }
    }

    submitMutation.mutate({
      command,
      pattern: pattern || undefined,
      priority: parseInt(priority),
      payload: parsedPayload,
    })
  }

  const handleClose = () => {
    setCommand('')
    setPattern('')
    setPriority('5')
    setPayload('')
    onClose()
  }

  if (!isOpen) return null

  return (
    <div className="modal-backdrop">
      <div className="modal-content w-full max-w-lg">
        <div className="p-6 border-b border-slate-200 dark:border-slate-700">
          <h2 className="text-xl font-bold text-slate-900 dark:text-white">Submit Job</h2>
          <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">
            Create a new job to be executed by an agent
          </p>
        </div>

        <div className="p-6 space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
              Command *
            </label>
            <input
              type="text"
              className="input w-full"
              placeholder="e.g., process-data, run-analysis"
              value={command}
              onChange={(e) => setCommand(e.target.value)}
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
              Agent Pattern (optional)
            </label>
            <input
              type="text"
              className="input w-full"
              placeholder="e.g., worker-*, group:production"
              value={pattern}
              onChange={(e) => setPattern(e.target.value)}
            />
            <p className="text-xs text-slate-500 mt-1">
              Leave empty to let the scheduler choose the best agent
            </p>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
              Priority (1-10)
            </label>
            <input
              type="number"
              min="1"
              max="10"
              className="input w-24"
              value={priority}
              onChange={(e) => setPriority(e.target.value)}
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
              Payload (JSON, optional)
            </label>
            <textarea
              className="input w-full h-32 font-mono text-sm"
              placeholder='{"key": "value"}'
              value={payload}
              onChange={(e) => setPayload(e.target.value)}
            />
          </div>
        </div>

        <div className="p-6 border-t border-slate-200 dark:border-slate-700 flex justify-end gap-3">
          <button onClick={handleClose} className="btn-secondary">
            Cancel
          </button>
          <button
            onClick={handleSubmit}
            disabled={!command.trim() || submitMutation.isPending}
            className="btn-primary"
          >
            {submitMutation.isPending ? 'Submitting...' : 'Submit Job'}
          </button>
        </div>
      </div>
    </div>
  )
}

export default function Jobs() {
  const [showSubmitModal, setShowSubmitModal] = useState(false)
  const [statusFilter, setStatusFilter] = useState<string>('')
  const queryClient = useQueryClient()

  const { data: jobs = [], isLoading } = useQuery({
    queryKey: ['jobs', statusFilter],
    queryFn: () => getJobs(statusFilter || undefined),
  })

  const cancelMutation = useMutation({
    mutationFn: cancelJob,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['jobs'] })
    },
  })

  const statusOptions: { value: string; label: string }[] = [
    { value: '', label: 'All Statuses' },
    { value: 'Pending', label: 'Pending' },
    { value: 'Running', label: 'Running' },
    { value: 'Completed', label: 'Completed' },
    { value: 'Failed', label: 'Failed' },
  ]

  const canCancel = (status: JobStatus) =>
    ['Pending', 'Assigned', 'Acknowledged', 'Running'].includes(status)

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 dark:text-white">Jobs</h1>
          <p className="text-slate-500 dark:text-slate-400 mt-1">View and manage job executions</p>
        </div>
        <div className="flex gap-3">
          <select
            className="input"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
          >
            {statusOptions.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
          <button
            onClick={() => queryClient.invalidateQueries({ queryKey: ['jobs'] })}
            className="btn-secondary flex items-center gap-2"
          >
            <RefreshCw className="w-4 h-4" />
            Refresh
          </button>
          <button
            onClick={() => setShowSubmitModal(true)}
            className="btn-primary flex items-center gap-2"
          >
            <Plus className="w-4 h-4" />
            Submit Job
          </button>
        </div>
      </div>

      {/* Jobs List */}
      <div className="card">
        {isLoading ? (
          <div className="text-center py-8">
            <RefreshCw className="w-8 h-8 text-slate-400 animate-spin mx-auto mb-2" />
            <p className="text-slate-500 dark:text-slate-400">Loading jobs...</p>
          </div>
        ) : jobs.length === 0 ? (
          <div className="text-center py-12">
            <PlayCircle className="w-12 h-12 text-slate-400 mx-auto mb-4" />
            <h3 className="text-lg font-medium text-slate-900 dark:text-white mb-2">No jobs found</h3>
            <p className="text-slate-500 dark:text-slate-400 mb-4">
              {statusFilter ? 'No jobs match the selected filter' : 'Submit your first job to get started'}
            </p>
            <button
              onClick={() => setShowSubmitModal(true)}
              className="btn-primary"
            >
              Submit Job
            </button>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="table-header">
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Job ID</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Command</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Status</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Agent</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Created</th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Progress</th>
                  <th className="text-right py-3 px-4 text-sm font-medium text-slate-500 dark:text-slate-400">Actions</th>
                </tr>
              </thead>
              <tbody>
                {jobs.map((job) => (
                  <tr key={job.id} className="table-row">
                    <td className="py-3 px-4">
                      <span className="text-xs text-slate-500 dark:text-slate-400 font-mono">
                        {job.id.substring(0, 8)}...
                      </span>
                    </td>
                    <td className="py-3 px-4">
                      <span className="text-slate-900 dark:text-white font-medium">{job.command}</span>
                    </td>
                    <td className="py-3 px-4">
                      <JobStatusBadge status={job.status} />
                    </td>
                    <td className="py-3 px-4">
                      <span className="text-slate-600 dark:text-slate-300 text-sm">
                        {job.agentId ? job.agentId.substring(0, 8) + '...' : '-'}
                      </span>
                    </td>
                    <td className="py-3 px-4">
                      <span className="text-sm text-slate-500 dark:text-slate-400">
                        {new Date(job.createdAt).toLocaleString()}
                      </span>
                    </td>
                    <td className="py-3 px-4">
                      {job.progress ? (
                        <div className="flex items-center gap-2">
                          <div className="w-24 h-2 bg-slate-200 dark:bg-slate-700 rounded-full overflow-hidden">
                            <div
                              className="h-full bg-orbit-500 transition-all"
                              style={{ width: `${job.progress.percentage}%` }}
                            />
                          </div>
                          <span className="text-xs text-slate-500 dark:text-slate-400">
                            {job.progress.percentage}%
                          </span>
                        </div>
                      ) : (
                        <span className="text-slate-400 dark:text-slate-500">-</span>
                      )}
                    </td>
                    <td className="py-3 px-4 text-right">
                      {canCancel(job.status) && (
                        <button
                          onClick={() => cancelMutation.mutate(job.id)}
                          disabled={cancelMutation.isPending}
                          className="text-red-600 hover:text-red-500 dark:text-red-400 dark:hover:text-red-300 text-sm"
                        >
                          Cancel
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Submit Job Modal */}
      <SubmitJobModal
        isOpen={showSubmitModal}
        onClose={() => setShowSubmitModal(false)}
      />
    </div>
  )
}
