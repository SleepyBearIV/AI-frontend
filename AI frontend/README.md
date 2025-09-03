# AI Chatbot Frontend (WPF)

This is a WPF-based frontend for an AI chatbot.

## Requirements
- .NET 8 SDK
- The backend service (FastAPI, Python) is required for this frontend to function.

## Backend Setup
You must run the backend server before starting the frontend.

- Backend repository: [https://github.com/SleepyBearIV/AI-Aigent](https://github.com/SleepyBearIV/AI-Aigent)
- Follow the instructions in the backend repo to install dependencies and start the FastAPI server.
- By default, the frontend expects the backend to be running at `http://localhost:8000`.

## Running the Frontend
1. Open the solution in Visual Studio.
2. Build and run the project.
3. Enter your message and interact with the AI chatbot.

## Features
- Modern, responsive WPF UI
- Connection status indicator (online/offline)
- "AI is typing..." bubble while waiting for response
- Dynamic chat layout

## Notes
- The frontend will not work without the backend running.
- You can customize backend URL in the code if needed.
