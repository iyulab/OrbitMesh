import { createFileRoute } from '@tanstack/react-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui'
import { useConnectionStore } from '@/stores/connection'
import { Activity, Server, Briefcase } from 'lucide-react'
import { cn } from '@/lib/utils'

export const Route = createFileRoute('/activity')({
  component: ActivityPage,
})

function ActivityPage() {
  const { recentAgentEvents, recentJobEvents, state } = useConnectionStore()

  const allEvents = [
    ...recentAgentEvents.map((e) => ({ ...e, category: 'agent' as const })),
    ...recentJobEvents.map((e) => ({ ...e, category: 'job' as const })),
  ].sort((a, b) => b.timestamp.getTime() - a.timestamp.getTime())

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold tracking-tight">Activity</h2>
        <p className="text-muted-foreground">
          Real-time events from your cluster
        </p>
      </div>

      {state !== 'connected' && (
        <Card className="border-yellow-500/50 bg-yellow-500/5">
          <CardContent className="flex items-center gap-3 py-4">
            <Activity className="h-5 w-5 text-yellow-500" />
            <p className="text-sm">
              {state === 'connecting' || state === 'reconnecting'
                ? 'Connecting to real-time updates...'
                : 'Not connected. Real-time updates are paused.'}
            </p>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Activity className="h-4 w-4" />
            Recent Events
          </CardTitle>
        </CardHeader>
        <CardContent>
          {allEvents.length === 0 ? (
            <div className="text-center py-12">
              <Activity className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
              <p className="text-lg font-medium">No events yet</p>
              <p className="text-sm text-muted-foreground mt-1">
                Events will appear here in real-time
              </p>
            </div>
          ) : (
            <div className="space-y-2">
              {allEvents.slice(0, 50).map((event, index) => (
                <div
                  key={`${event.category}-${event.category === 'agent' ? event.agentId : event.jobId}-${event.timestamp.getTime()}-${index}`}
                  className="flex items-center gap-3 p-3 rounded-lg hover:bg-muted/50 transition-colors"
                >
                  <div
                    className={cn(
                      'flex h-8 w-8 items-center justify-center rounded-full',
                      event.category === 'agent'
                        ? 'bg-blue-500/10 text-blue-500'
                        : 'bg-purple-500/10 text-purple-500'
                    )}
                  >
                    {event.category === 'agent' ? (
                      <Server className="h-4 w-4" />
                    ) : (
                      <Briefcase className="h-4 w-4" />
                    )}
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium">
                      {event.category === 'agent' ? (
                        <>
                          Agent{' '}
                          <span className="font-mono text-muted-foreground">
                            {event.agentId.slice(0, 8)}...
                          </span>{' '}
                          {event.type === 'connected' && 'connected'}
                          {event.type === 'disconnected' && 'disconnected'}
                          {event.type === 'statusChanged' && 'status changed'}
                        </>
                      ) : (
                        <>
                          Job{' '}
                          <span className="font-mono text-muted-foreground">
                            {event.jobId.slice(0, 8)}...
                          </span>{' '}
                          {event.type === 'created' && 'created'}
                          {event.type === 'statusChanged' && 'status changed'}
                          {event.type === 'completed' && 'completed'}
                        </>
                      )}
                    </p>
                  </div>
                  <span className="text-xs text-muted-foreground">
                    {formatRelativeTime(event.timestamp)}
                  </span>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

function formatRelativeTime(date: Date): string {
  const seconds = Math.floor((Date.now() - date.getTime()) / 1000)

  if (seconds < 5) return 'just now'
  if (seconds < 60) return `${seconds}s ago`
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`
  if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`
  return `${Math.floor(seconds / 86400)}d ago`
}
