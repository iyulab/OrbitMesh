import { createFileRoute, Link } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { getJobs } from '@/lib/api'
import { Card, CardContent, JobStatusBadge } from '@/components/ui'
import { Briefcase, ExternalLink } from 'lucide-react'
import { formatDate, formatDuration } from '@/lib/utils'

export const Route = createFileRoute('/jobs')({
  component: JobsPage,
})

function JobsPage() {
  const { data, isLoading, error } = useQuery({
    queryKey: ['jobs'],
    queryFn: () => getJobs({ pageSize: 50 }),
    refetchInterval: 5000,
  })

  if (isLoading) {
    return <JobsSkeleton />
  }

  if (error) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-center">
          <p className="text-muted-foreground">Failed to load jobs</p>
          <p className="text-sm text-destructive mt-2">
            {error instanceof Error ? error.message : 'Unknown error'}
          </p>
        </div>
      </div>
    )
  }

  const jobs = data?.items ?? []

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold tracking-tight">Jobs</h2>
          <p className="text-muted-foreground">
            {data?.totalCount ?? 0} jobs total
          </p>
        </div>
      </div>

      {jobs.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12">
            <Briefcase className="h-12 w-12 text-muted-foreground mb-4" />
            <p className="text-lg font-medium">No jobs yet</p>
            <p className="text-sm text-muted-foreground mt-1">
              Jobs will appear here when they are created
            </p>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="border-b">
                  <th className="px-4 py-3 text-left text-sm font-medium text-muted-foreground">
                    Command
                  </th>
                  <th className="px-4 py-3 text-left text-sm font-medium text-muted-foreground">
                    Status
                  </th>
                  <th className="px-4 py-3 text-left text-sm font-medium text-muted-foreground">
                    Agent
                  </th>
                  <th className="px-4 py-3 text-left text-sm font-medium text-muted-foreground">
                    Duration
                  </th>
                  <th className="px-4 py-3 text-left text-sm font-medium text-muted-foreground">
                    Created
                  </th>
                  <th className="px-4 py-3 text-right text-sm font-medium text-muted-foreground">
                    Actions
                  </th>
                </tr>
              </thead>
              <tbody>
                {jobs.map((job) => {
                  const duration =
                    job.startedAt && job.completedAt
                      ? new Date(job.completedAt).getTime() -
                        new Date(job.startedAt).getTime()
                      : null

                  return (
                    <tr
                      key={job.id}
                      className="border-b last:border-0 hover:bg-muted/50 transition-colors"
                    >
                      <td className="px-4 py-3">
                        <div>
                          <p className="font-medium">{job.request.command}</p>
                          <p className="text-xs text-muted-foreground font-mono">
                            {job.id.slice(0, 8)}...
                          </p>
                        </div>
                      </td>
                      <td className="px-4 py-3">
                        <JobStatusBadge status={job.status} />
                      </td>
                      <td className="px-4 py-3">
                        {job.assignedAgentId ? (
                          <Link
                            to="/agents/$agentId"
                            params={{ agentId: job.assignedAgentId }}
                            className="text-sm text-primary hover:underline"
                          >
                            {job.assignedAgentId.slice(0, 8)}...
                          </Link>
                        ) : (
                          <span className="text-sm text-muted-foreground">-</span>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        <span className="text-sm">
                          {duration ? formatDuration(duration) : '-'}
                        </span>
                      </td>
                      <td className="px-4 py-3">
                        <span className="text-sm text-muted-foreground">
                          {formatDate(job.createdAt)}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-right">
                        <Link
                          to="/jobs/$jobId"
                          params={{ jobId: job.id }}
                          className="inline-flex items-center gap-1 text-sm text-primary hover:underline"
                        >
                          View
                          <ExternalLink className="h-3 w-3" />
                        </Link>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </Card>
      )}
    </div>
  )
}

function JobsSkeleton() {
  return (
    <div className="space-y-6">
      <div>
        <div className="h-8 w-16 bg-muted rounded animate-pulse" />
        <div className="h-4 w-32 bg-muted rounded animate-pulse mt-2" />
      </div>
      <Card>
        <div className="p-4 space-y-4">
          {[...Array(5)].map((_, i) => (
            <div key={i} className="h-12 bg-muted rounded animate-pulse" />
          ))}
        </div>
      </Card>
    </div>
  )
}
