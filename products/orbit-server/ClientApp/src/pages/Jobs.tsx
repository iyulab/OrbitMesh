import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  PlayCircle,
  Plus,
  RefreshCw,
  ChevronRight,
  AlertCircle,
} from 'lucide-react'
import { getJobs, submitJob, cancelJob } from '@/api/client'
import type { JobStatus } from '@/types'
import { JobStatusBadge } from '@/components/ui/status-badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { toast } from '@/components/ui/sonner'

function SubmitJobDialog({
  open,
  onOpenChange,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const [command, setCommand] = useState('')
  const [pattern, setPattern] = useState('')
  const [priority, setPriority] = useState('5')
  const [payload, setPayload] = useState('')
  const [payloadError, setPayloadError] = useState<string | null>(null)

  const queryClient = useQueryClient()

  const submitMutation = useMutation({
    mutationFn: submitJob,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['jobs'] })
      toast.success('Job submitted successfully')
      handleClose()
    },
    onError: (error) => {
      toast.error('Failed to submit job', {
        description: error instanceof Error ? error.message : 'Unknown error',
      })
    },
  })

  const handleSubmit = () => {
    let parsedPayload: object | undefined
    if (payload.trim()) {
      try {
        parsedPayload = JSON.parse(payload)
        setPayloadError(null)
      } catch {
        setPayloadError('Invalid JSON format')
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
    setPayloadError(null)
    onOpenChange(false)
  }

  return (
    <Dialog open={open} onOpenChange={(o) => !o && handleClose()}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Submit Job</DialogTitle>
          <DialogDescription>
            Create a new job to be executed by an agent
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <Label htmlFor="command">Command *</Label>
            <Input
              id="command"
              placeholder="e.g., process-data, run-analysis"
              value={command}
              onChange={(e) => setCommand(e.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="pattern">Agent Pattern (optional)</Label>
            <Input
              id="pattern"
              placeholder="e.g., worker-*, group:production"
              value={pattern}
              onChange={(e) => setPattern(e.target.value)}
            />
            <p className="text-xs text-muted-foreground">
              Leave empty to let the scheduler choose the best agent
            </p>
          </div>

          <div className="space-y-2">
            <Label htmlFor="priority">Priority (1-10)</Label>
            <Input
              id="priority"
              type="number"
              min="1"
              max="10"
              className="w-24"
              value={priority}
              onChange={(e) => setPriority(e.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="payload">Payload (JSON, optional)</Label>
            <Textarea
              id="payload"
              className="h-32 font-mono text-sm"
              placeholder='{"key": "value"}'
              value={payload}
              onChange={(e) => {
                setPayload(e.target.value)
                setPayloadError(null)
              }}
            />
            {payloadError && (
              <Alert variant="destructive" className="py-2">
                <AlertCircle className="w-4 h-4" />
                <AlertDescription>{payloadError}</AlertDescription>
              </Alert>
            )}
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleClose}>
            Cancel
          </Button>
          <Button
            onClick={handleSubmit}
            disabled={!command.trim() || submitMutation.isPending}
          >
            {submitMutation.isPending ? 'Submitting...' : 'Submit Job'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export default function Jobs() {
  const [showSubmitDialog, setShowSubmitDialog] = useState(false)
  const [statusFilter, setStatusFilter] = useState<string>('all')
  const queryClient = useQueryClient()

  const { data: jobs = [], isLoading } = useQuery({
    queryKey: ['jobs', statusFilter],
    queryFn: () => getJobs(statusFilter === 'all' ? undefined : statusFilter),
  })

  const cancelMutation = useMutation({
    mutationFn: cancelJob,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['jobs'] })
      toast.success('Job cancelled')
    },
    onError: (error) => {
      toast.error('Failed to cancel job', {
        description: error instanceof Error ? error.message : 'Unknown error',
      })
    },
  })

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
          <Select value={statusFilter} onValueChange={setStatusFilter}>
            <SelectTrigger className="w-40">
              <SelectValue placeholder="Filter by status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Statuses</SelectItem>
              <SelectItem value="Pending">Pending</SelectItem>
              <SelectItem value="Running">Running</SelectItem>
              <SelectItem value="Completed">Completed</SelectItem>
              <SelectItem value="Failed">Failed</SelectItem>
            </SelectContent>
          </Select>
          <Button
            variant="outline"
            onClick={() => queryClient.invalidateQueries({ queryKey: ['jobs'] })}
          >
            <RefreshCw className="w-4 h-4 mr-2" />
            Refresh
          </Button>
          <Button onClick={() => setShowSubmitDialog(true)}>
            <Plus className="w-4 h-4 mr-2" />
            Submit Job
          </Button>
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
              {statusFilter !== 'all' ? 'No jobs match the selected filter' : 'Submit your first job to get started'}
            </p>
            <Button onClick={() => setShowSubmitDialog(true)}>
              Submit Job
            </Button>
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
                  <tr key={job.id} className="table-row hover:bg-slate-50 dark:hover:bg-slate-800/50">
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
                      <div className="flex items-center justify-end gap-2">
                        {canCancel(job.status) && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => cancelMutation.mutate(job.id)}
                            disabled={cancelMutation.isPending}
                            className="text-red-600 hover:text-red-500 dark:text-red-400"
                          >
                            Cancel
                          </Button>
                        )}
                        <Link to={`/jobs/${job.id}`}>
                          <Button variant="ghost" size="sm">
                            <ChevronRight className="w-4 h-4" />
                          </Button>
                        </Link>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Submit Job Dialog */}
      <SubmitJobDialog
        open={showSubmitDialog}
        onOpenChange={setShowSubmitDialog}
      />
    </div>
  )
}
