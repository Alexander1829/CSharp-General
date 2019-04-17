/*SOLID - аббревиатура для паттерна ООП разработки. В C# выглядит нижеследующим образом:*/
//Начну с "D". А дальше по порядку.
namespace SOLID_in_CSharp
{
    /*Dependency Injection. 
    Если нам от вызываемого класса нужны только какой-то(какие-то)
    конкретные методы или проперти. То используем , вместо прямой зависимости класса от класса, 
    зависимость класса от интерфейса с этими методами и полями.
    И в конструкторе вызывающего класса добавляем аргумент типа этого интерфейса. */

    /*Например, нужен метод Send() от экземпляра типа 
    SMS или Email или ... Заменяем на тип интерфейс IMessenger{ void Send(); }  */
    public class Notification
    {
        private IMessenger _messenger;
        public Notification(IMessenger mes)
        {
            _messenger = mes;
        }

        public void DoNotify()
        {
            _messenger.Send();
        }
    }
    public interface IMessenger
    {
        void Send();
    }


    public class Email : IMessenger
    {
        public void Send()
        {
            // код для отправки email-письма
        }
    }

    public class SMS : IMessenger
    {
        public void Send()
        {
            // код для отправки SMS
        }
    }
    /*Dependency Injection. End */

    /* Single Responsibility. Суть: "Одна метасущность - один класс".  */
    public class Employee
    {
        public int ID { get; set; }
        public string FullName { get; set; }

        // Добавить в БД нового сотрудника
        public bool Add(Employee emp)
        {
            // Вставить данные сотрудника в таблицу БД
            return true;
        }
    }

    /*Другая метаcущность: строитель отчётов ( по сотрудникам )*/
    public class EmployeeReport
    {
        // Отчет по сотруднику
        public void GenerateReport(Employee em)
        {
            // ... 
        }
    }
    /* Single Responsibility. End */


    /* Open Closed Principle. На самом деле это - инкапсуляция, 
    только SILID - не звучит так красно :) */

    /* Пример: */
    public class EmpReport
    {
        // Метод для создания отчета
        public virtual void GenerateReport(Employee em)
        {
            // Базовая реализация, которую нельзя модифицировать
        }
    }

    public class EmployeeCSVReport : EmpReport
    {
        public override void GenerateReport(Employee em)
        {
            // Генерация отчета в формате CSV
        }
    }

    public class EmployeePDFReport : EmpReport
    {
        public override void GenerateReport(Employee em)
        {
            // Генерация отчета в формате PDF
        }
    }
    /* Open Closed Principle. End */


    /*Liskov Childhood Principle. Реализация дочернего класса не должна
     логически противоречить реализации родительского класса.*/

    //очевидно

    /*Liskov Childhood Principle. End */


    /*Interface Segregation. Стараемся, за единичными исключениями, не
    перегружать интерфейсы. Стационарно: один интерфейс - один метод.
    И никогда НЕ добавляем в уже ИСПОЛЬЗУЮЩИЙСЯ ИНТЕРФЕЙС НОВЫЕ декларации.*/

    /* Пример: */
    //Есть интерфейс IEmployee с методом AddDetailsEmployee()
    public interface IEmployee
    {
        bool AddDetailsEmployee();
    }

    /*Так НЕ ДЕЛАЕМ:
     public interface IEmployee
    {
        bool AddDetailsEmployee();
        bool ShowDetailsEmployee(int id);
    }
     все классы, реализовавшие IEmployee, ломаются.*/

    /*Так делаем: */
    public interface IOperationAdd
    {
        bool AddDetailsEmployee();
    }

    public interface IOperationGet
    {
        bool ShowDetailsEmployee(int id);
    }

}

//Пример для корректировки под стандарты Solid
namespace WrangCode
{
    /* Single Responsibility. WRANG: Две  логически несвязанные метасущности - в одном классе!  */
    public class Employee
    {
        public int ID { get; set; }
        public string FullName { get; set; }

        public bool Add(Employee emp)
        {
            // Вставить данные сотрудника в таблицу БД
            return true;
        }

        public void GenerateReport(Employee em)
        {
            // Генерация отчета по деятельности сотрудника
        }
    }
    /* Single Responsibility. End */
}



