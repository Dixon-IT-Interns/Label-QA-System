pipeline {
    agent any
    
    stages {
        stage('Checkout') {
            steps {
                checkout scm
                bat 'git branch --show-current'
            }
        }
        stage('Backend Build') {
            steps {
                bat '''
                cd backend\\Dixon.LabelVerification.API
                "C:\\Users\\Dxnintents\\.dotnet\\dotnet.exe" publish -c Release -o C:\\Deploy\\LabelQA\\backend
                '''
            }
        }
        stage('Frontend Build') {
            steps {
                bat '''
                cd "New Frontend"
                npm install
                npm run build
                if exist C:\\Deploy\\LabelQA\\frontend rmdir /S /Q C:\\Deploy\\LabelQA\\frontend
                mkdir C:\\Deploy\\LabelQA\\frontend
                xcopy /E /Y /I dist C:\\Deploy\\LabelQA\\frontend
                '''
            }
        }
        stage('Deploy') {
            steps {
                bat 'powershell -ExecutionPolicy Bypass -File jenkins\\deploy.ps1'
            }
        }
    }
    post {
        success {
            echo 'Deploy SUCCESS - http://10.52.2.78:5173 pe check karo'
        }
        failure {
            echo 'Deploy FAILED - Console Output me error dekho'
        }
    }
}