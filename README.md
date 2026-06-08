# MyMcpProject 

A full-stack .NET application that utilizes the **Model Context Protocol (MCP)** to allow an AI model (Google AI Studio) to intelligently query and write data across three completely different database architectures.


🏗️ Project Architecture & Data Flow

1. **Frontend:** A user inputs a natural language question through an AJAX web interface.
2. **Proxy MCP Server (.NET):** Passes the request to the LLM (Google AI Studio).
3. **AI Studio:** Decides which database holds the relevant information and selects the correct tool (function).
4. **Execution:** The Proxy server runs the selected read/write operation against the targeted database and returns the final natural language answer back to the user.

<img width="500"  alt="image" src="https://github.com/user-attachments/assets/6ce1939a-363a-4299-8066-8070e4c416f0" />


📊 Connected Databases

The system manages three distinct data domains, each running on a unique backend technology:

1. **Crime Reports & Evidence** (REST API via `json-server` - localhost:4000)
   * Managed using flat JSON files exposed as REST endpoints.
2. **Cartoons & Characters** (GraphQL Server - localhost:3000)
   * Managed via structured graph query runtime endpoints.
3. **Authors & Books** (RDF4J Semantic Graph Database - localhost:8080, name: grafexamen)
   * Managed via semantic web triples and graph relations.


🤖 Supported AI Questions 

The AI is programmed to handle the following operations automatically based on your prompt:

🔍 Read Operations (With Filtering)
* **Crime Data:** `"Get all evidence of type [Murder / Theft]"`
* **Cartoon Data:** `"Get all cartoons created before year [Year]"`
* **Book Data:** `"Get all books published after year [Year]"`

📝 Write Operations
* **Crime Data:** `"Add an evidence to crime report with id [ID]"`
* **Cartoon Data:** `"Add a new character to the cartoon [Cartoon id]"`
* **Book Data:** `"Insert a book to author [Author name]"`
