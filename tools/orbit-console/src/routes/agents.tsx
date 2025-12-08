import { createFileRoute, Link } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { getAgents } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle, AgentStatusBadge } from '@/components/ui'
import { Server, ExternalLink } from 'lucide-react'
import { formatDate } from '@/lib/utils'

export const Route = createFileRoute('/agents')({
  component: AgentsPage,
})

function AgentsPage() {
  const { data, isLoading, error } = useQuery({
    queryKey: ['agents'],
    queryFn: () => getAgents({ pageSize: 50 }),
    refetchInterval: 10000,
  })

  if (isLoading) {
    return <AgentsSkeleton />
  }

  if (error) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-center">
          <p className="text-muted-foreground">Failed to load agents</p>
          <p className="text-sm text-destructive mt-2">
            {error instanceof Error ? error.message : 'Unknown error'}
          </p>
        </div>
      </div>
    )
  }

  const agents = data?.items ?? []

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold tracking-tight">Agents</h2>
          <p className="text-muted-foreground">
            {data?.totalCount ?? 0} agents registered
          </p>
        </div>
      </div>

      {agents.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12">
            <Server className="h-12 w-12 text-muted-foreground mb-4" />
            <p className="text-lg font-medium">No agents registered</p>
            <p className="text-sm text-muted-foreground mt-1">
              Start an OrbitAgent to see it here
            </p>
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {agents.map((agent) => (
            <Card key={agent.id} className="hover:border-primary/50 transition-colors">
              <CardHeader className="pb-3">
                <div className="flex items-start justify-between">
                  <div className="space-y-1">
                    <CardTitle className="text-base">{agent.name}</CardTitle>
                    <p className="text-xs text-muted-foreground font-mono">
                      {agent.id}
                    </p>
                  </div>
                  <AgentStatusBadge status={agent.status} />
                </div>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="grid grid-cols-2 gap-2 text-sm">
                  {agent.hostname && (
                    <div>
                      <p className="text-muted-foreground text-xs">Hostname</p>
                      <p className="font-medium truncate">{agent.hostname}</p>
                    </div>
                  )}
                  {agent.group && (
                    <div>
                      <p className="text-muted-foreground text-xs">Group</p>
                      <p className="font-medium">{agent.group}</p>
                    </div>
                  )}
                  {agent.version && (
                    <div>
                      <p className="text-muted-foreground text-xs">Version</p>
                      <p className="font-medium">{agent.version}</p>
                    </div>
                  )}
                  {agent.lastHeartbeat && (
                    <div>
                      <p className="text-muted-foreground text-xs">Last seen</p>
                      <p className="font-medium text-xs">
                        {formatDate(agent.lastHeartbeat)}
                      </p>
                    </div>
                  )}
                </div>

                {agent.capabilities.length > 0 && (
                  <div>
                    <p className="text-muted-foreground text-xs mb-1">Capabilities</p>
                    <div className="flex flex-wrap gap-1">
                      {agent.capabilities.slice(0, 3).map((cap) => (
                        <span
                          key={cap}
                          className="inline-flex items-center rounded bg-muted px-2 py-0.5 text-xs"
                        >
                          {cap}
                        </span>
                      ))}
                      {agent.capabilities.length > 3 && (
                        <span className="text-xs text-muted-foreground">
                          +{agent.capabilities.length - 3} more
                        </span>
                      )}
                    </div>
                  </div>
                )}

                <div className="pt-2 border-t">
                  <Link
                    to="/agents/$agentId"
                    params={{ agentId: agent.id }}
                    className="text-sm text-primary hover:underline inline-flex items-center gap-1"
                  >
                    View details
                    <ExternalLink className="h-3 w-3" />
                  </Link>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}

function AgentsSkeleton() {
  return (
    <div className="space-y-6">
      <div>
        <div className="h-8 w-24 bg-muted rounded animate-pulse" />
        <div className="h-4 w-40 bg-muted rounded animate-pulse mt-2" />
      </div>
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {[...Array(6)].map((_, i) => (
          <Card key={i}>
            <CardHeader className="pb-3">
              <div className="h-5 w-32 bg-muted rounded animate-pulse" />
              <div className="h-3 w-48 bg-muted rounded animate-pulse mt-1" />
            </CardHeader>
            <CardContent>
              <div className="h-20 bg-muted rounded animate-pulse" />
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}
