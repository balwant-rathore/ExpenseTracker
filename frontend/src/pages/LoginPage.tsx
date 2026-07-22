import { Link } from 'react-router-dom'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { LoginForm } from '@/features/auth/components/LoginForm'

export function LoginPage() {
  return (
    <div className="flex min-h-svh items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Log in</CardTitle>
          <CardDescription>Enter your email and password to access Expense Tracker.</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <LoginForm />
          <p className="text-sm text-muted-foreground">
            Don&apos;t have an account?{' '}
            <Link to="/register" className="underline underline-offset-4">
              Register
            </Link>
          </p>
          <p className="text-sm text-muted-foreground">
            <Link to="/forgot-password" className="underline underline-offset-4">
              Forgot your password?
            </Link>
          </p>
        </CardContent>
      </Card>
    </div>
  )
}
