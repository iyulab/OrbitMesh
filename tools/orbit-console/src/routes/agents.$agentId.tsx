import { createFileRoute, Link } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { getAgent, getJobs } from '@/lib/api'
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  AgentStatusBadge,
  JobStatusBadge,
} from '@/components/ui'
import { ArrowLeft, Server, Clock, Tag } from 'lucide-react'
import { formatDate } from '@/lib/utils'

export const Route = createFileRoute('/agents/$agentId')({
  component: AgentDetailPage,
})

function AgentDetailPage() {
  const { agentId } = Route.useParams()

  const { data: agent, isLoading: agentLoading } = useQuery({
    queryKey: ['agent', agentId],
    queryFn: () => getAgent(agentId),
  })

  const { data: jobs } = useQuery({
    queryKey: ['jobs', { agentId }],
    queryFn: () => getJobs({ agentId, pageSize: 10 }),
    enabled: !!agent,
  })

  if (agentLoading) {
    return <AgentDetailSkeleton />
  }

  if (!agent) {
    return (
      <div className="space-y-6">
        <Link
          to="/agents"
          className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to agents
        </Link>
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12">
            <p className="text-lg font-medium">Agent not found</p>
          </CardContent>
        </Card>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <Link
        to="/agents"
        className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="h-4 w-4" />
        Back to agents
      </Link>

      {/* Agent header */}
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-4">
          <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-primary/10">
            <Server className="h-6 w-6 text-primary" />
          </div>
          <div>
            <h2 className="text-2xl font-bold">{agent.name}</h2>
            <p className="text-sm text-muted-foreground font-mono">{agent.id}</p>
          </div>
        </div>
        <AgentStatusBadge status={agent.status} />
      </div>

      {/* Agent details */}
      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-sm">Details</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <DetailRow label="Hostname" value={agent.hostname} />
            <DetailRow label="Group" value={agent.group} />
            <DetailRow label="Version" value={agent.version} />
            <DetailRow
              label="Registered"
              value={formatDate(agent.registeredAt)}
            />
            <DetailRow
              label="Last Heartbeat"
              value={agent.lastHeartbeat ? formatDate(agent.lastHeartbeat) : '-'}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-sm">
              <Tag className="h-4 w-4" />
              Capabilities & Tags
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {agent.capabilities.length > 0 && (
              <div>
                <p className="text-xs text-muted-foreground mb-2">Capabilities</p>
                <div className="flex flex-wrap gap-1">
                  {agent.capabilities.map((cap) => (
                    <span
                      key={cap}
                      className="inline-flex items-center rounded bg-primary/10 text-primary px-2 py-0.5 text-xs"
                    >
                      {cap}
                    </span>
                  ))}
                </div>
              </div>
            )}
            {agent.tags.length > 0 && (
              <div>
                <p className="text-xs text-muted-foreground mb-2">Tags</p>
                <div className="flex flex-wrap gap-1">
                  {agent.tags.map((tag) => (
                    <span
                      key={tag}
                      className="inline-flex items-center rounded bg-muted px-2 py-0.5 text-xs"
                    >
                      {tag}
                    </span>
                  ))}
                </div>
              </div>
            )}
            {agent.capabilities.length === 0 && agent.tags.length === 0 && (
              <p className="text-sm text-muted-foreground">
                No capabilities or tags defined
              </p>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Recent jobs */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Clock className="h-4 w-4" />
            Recent Jobs
          </CardTitle>
        </CardHeader>
        <CardContent>
          {jobs?.items && jobs.items.length > 0 ? (
            <div className="space-y-2">
              {jobs.items.map((job) => (
                <Link
                  key={job.id}
                  to="/jobs/$jobId"
                  params={{ jobId: job.id }}
                  className="flex items-center justify-between p-3 rounded-lg hover:bg-muted transition-colors"
                >
                  <div>
                    <p className="font-medium">{job.request.command}</p>
                    <p className="text-xs text-muted-foreground font-mono">
                      {job.id}
                    </p>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="text-xs text-muted-foreground">
                      {formatDate(job.createdAt)}
                    </span>
                    <JobStatusBadge status={job.status} />
                  </div>
                </Link>
              ))}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground text-center py-8">
              No jobs assigned to this agent
            </p>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

function DetailRow({
  label,
  value,
}: {
  label: string
  value: string | undefined | null
}) {
  return (
    <div className="flex justify-between">
      <span className="text-sm text-muted-foreground">{label}</span>
      <span className="text-sm font-medium">{value || '-'}</span>
    </div>
  )
}

function AgentDetailSkeleton() {
  return (
    <div className="space-y-6">
      <div className="h-4 w-24 bg-muted rounded animate-pulse" />
      <div className="flex items-center gap-4">
        <div className="h-12 w-12 bg-muted rounded-lg animate-pulse" />
        <div>
          <div className="h-8 w-48 bg-muted rounded animate-pulse" />
          <div className="h-4 w-64 bg-muted rounded animate-pulse mt-2" />
        </div>
      </div>
      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardContent className="h-48 animate-pulse bg-muted/50" />
        </Card>
        <Card>
          <CardContent className="h-48 animate-pulse bg-muted/50" />
        </Card>
      </div>
    </div>
  )
}
