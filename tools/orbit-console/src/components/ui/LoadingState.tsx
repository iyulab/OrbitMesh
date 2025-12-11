import { cn } from '@/lib/utils'

interface LoadingStateProps {
  className?: string
  text?: string
}

export function LoadingState({ className, text = 'Loading...' }: LoadingStateProps) {
  return (
    <div className={cn('flex flex-col items-center justify-center py-12', className)}>
      <div className="h-8 w-8 animate-spin rounded-full border-2 border-muted border-t-primary" />
      <p className="text-sm text-muted-foreground mt-4">{text}</p>
    </div>
  )
}

interface LoadingSkeletonProps {
  className?: string
}

export function LoadingSkeleton({ className }: LoadingSkeletonProps) {
  return <div className={cn('bg-muted rounded animate-pulse', className)} />
}
